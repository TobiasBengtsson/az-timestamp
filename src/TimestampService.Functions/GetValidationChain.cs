using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using TimestampService.Functions.Data;
using TimestampService.Functions.Dto;
using System.Linq;
using TimestampService.Functions.Extensions;

namespace TimestampService.Functions
{
    public static class GetValidationChain
    {
        /// <summary>
        /// Gets a validation chain for a hash.
        /// </summary>
        /// <remarks>
        /// This endpoint is for validating a hash that has been previously posted. The response will include the chain hashes
        /// and how they were calculated from the hash up until the closest following publish to OriginStamp, providing proof that
        /// the hash was indeed posted before the time it was published to OriginStamp.
        ///
        /// If the hash has not been part of a publish yet, the response will indicate so by returning includedInChain=false (in WaitingForProcessing state)
        /// and/or validated=false (included in chain but tip of chain not published yet).
        /// </remarks>
        [FunctionName("GetValidationChain")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "{hash}/validationChain")] HttpRequest req,
            string hash,
            [Table("timestamp")] CloudTable timestampTable,
            ILogger log)
        {
            log.LogInformation("Getting validation chain for hash: '{hash}'...", hash);
            var repository = new TimestampRepository(timestampTable);
            var hashEntry = await repository.GetHash(hash);
            if (hashEntry == null)
            {
                return new NotFoundResult();
            }

            var response = new ValidationChainDto
            {
                Hash = hashEntry.RowKey
            };

            if (!hashEntry.ChainId.HasValue)
            {
                return new OkObjectResult(response);
            }

            response.IncludedInChain = true;
            response.ChainId = hashEntry.ChainId;

            if (!hashEntry.PublishedOriginStampChainId.HasValue)
            {
                return new OkObjectResult(response);
            }

            response.Validated = true;
            response.ValidatedChainId = hashEntry.PublishedOriginStampChainId;

            var chain = await repository.GetPartOfChain(Math.Max(1, hashEntry.ChainId.Value - 1),
                hashEntry.PublishedOriginStampChainId.Value);

            var validationChain = chain.Select(chainItem => new ValidationChainEntryDto
            {
                ChainId = chainItem.ChainId,
                ChainHash = chainItem.ChainFingerprint.ToHexString(),
                Hash = chainItem.ItemFingerprint.ToHexString(),
                HashedValue = chainItem.HashedValue.ToHexString()
            }).ToList();

            response.ValidationChain = validationChain;
            response.ValidatedHash = validationChain.Last().ChainHash;

            return new OkObjectResult(response);
        }
    }
}
