using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using TimestampService.Functions.Data;
using TimestampService.Functions.DataModel;
using TimestampService.Functions.Extensions;

namespace TimestampService.Functions
{
    public static class AddTimestamp
    {
        [FunctionName("AddTimestamp")]
        public static async Task Run(
            [QueueTrigger("timestamp-incoming")] string hash,
            [Table("timestamp")] CloudTable timestampTable,
            ILogger log)
        {
            log.LogInformation($"Starting to process hash: {hash}...");
            if (!HashValidator.IsValidHash(hash))
            {
                log.LogError("Hash is invalid.");
                throw new Exception("Hash is invalid.");
            }

            var repository = new TimestampRepository(timestampTable);

            if (await repository.HashExists(hash))
            {
                log.LogInformation("Hash already added.");
                return;
            }

            await repository.AddHash(hash);
            var bytes = hash.ToByteArray();
            await repository.AddWaitForProcessing(new WaitingForProcessingTimestamp
            {
                PartitionKey = "WaitingForProcessing",
                RowKey = DateTime.Now.Ticks.ToString(),
                Fingerprint = bytes
            });

            return;
        }
    }
}
