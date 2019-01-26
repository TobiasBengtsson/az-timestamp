using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using TimestampService.Functions.Data;
using TimestampService.Functions.DataModel;
using TimestampService.Functions.Extensions;

namespace TimestampService.Functions
{
    public static class ProcessNewlyAddedHashes
    {
        /// <summary>
        /// Inserts all items that are marked as "WaitingForProcessing" to the hash chain.
        /// </summary>
        [FunctionName("ProcessNewlyAddedHashes")]
        public static async Task RunAsync(
            [TimerTrigger("0 0 * * * *")] TimerInfo myTimer,
            [Table("timestamp")] CloudTable timestampTable,
            ILogger log)
        {
            log.LogInformation($"Started processing newly added hashes at {DateTime.Now}...");

            var repository = new TimestampRepository(timestampTable);

            // No need to batch here for the expected load.
            var itemsToProcess = await repository.GetAllWaitingForProcessing();

            if (!itemsToProcess.Any())
            {
                log.LogInformation("No new items to process.");
                return;
            }
            else
            {
                log.LogInformation("{itemCount} items waiting to be processed found.", itemsToProcess.Count());
            }

            var latestProcessedTimestamp = await repository.GetLastestProcessedTimestamp();
            if (latestProcessedTimestamp != null)
            {
                log.LogInformation("Latest processed timestamp ID: {chainId}, Chained hash: {chainHash}.",
                    latestProcessedTimestamp.ChainId,
                    BitConverter.ToString(latestProcessedTimestamp.ChainFingerprint).Replace("-", ""));
            }
            else
            {
                log.LogInformation($"First timestamp ever. Nice!");
                latestProcessedTimestamp = new ProcessedTimestamp
                {
                    ChainId = 0,
                    ChainFingerprint = new byte[0]
                };
            }

            foreach (var itemToProcess in itemsToProcess)
            {
                var sha256 = SHA256.Create();

                // For each item, the hash is concatenated with the previous chain hash (chain hash + item hash)
                // and the hash of this concatenation is hashed in turn as the new chain hash.
                var hashedValue = latestProcessedTimestamp.ChainFingerprint
                    .Concat(itemToProcess.Fingerprint).ToArray();
                var newHash = sha256.ComputeHash(hashedValue);

                var newChainId = latestProcessedTimestamp.ChainId + 1;
                var processedTimestamp = new ProcessedTimestamp
                {
                    PartitionKey = "ProcessedTimestamp",

                    // Inversion to keep latest (largest) ID's at top of table for performance when fetching
                    RowKey = (int.MaxValue - newChainId).ToString(),
                    ChainId = newChainId,
                    ChainFingerprint = newHash,
                    HashedValue = hashedValue,
                    ItemFingerprint = itemToProcess.Fingerprint,
                    TimeAddedToQueue = itemToProcess.Timestamp
                };

                await repository.AddProcessedTimestamp(processedTimestamp);
                await repository.RemoveWaitingForProcessingTimestamp(itemToProcess);
                await repository.AddChainIdToHash(itemToProcess.Fingerprint.ToHexString(), newChainId);
                latestProcessedTimestamp = processedTimestamp;

                log.LogInformation("Processed hash {fingerprint}. Used hash value {hashedValue} for calculating chain hash. Calculated chained hash is {newHash}. ID in chain is {newChainId}.",
                    itemToProcess.Fingerprint.ToHexString(), hashedValue.ToHexString(), newHash.ToHexString(), newChainId);
            }

            return;
        }
    }
}
