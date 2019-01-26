using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using SendGrid;
using SendGrid.Helpers.Mail;
using TimestampService.Functions.Data;
using TimestampService.Functions.DataModel;
using TimestampService.Functions.Extensions;

namespace TimestampService.Functions
{
    public static class PublishTipOfHashChain
    {
        /// <summary>
        /// Takes the latest chain hash and publishes it to OriginStamp.
        /// </summary>
        /// <remarks>
        /// After successful publish, an email is sent specifying the chain hash that was published,
        /// all hashes included since the last publish and how the chain hash was calculated.
        /// </remarks>
        [FunctionName("PublishTipOfHashChain")]
        public static async Task RunAsync(
            [TimerTrigger("0 30 0 * * *")] TimerInfo myTimer,
            [Table("timestamp")] CloudTable timestampTable,
            ExecutionContext executionContext,
            ILogger log)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(executionContext.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var repository = new TimestampRepository(timestampTable);
            var latestTimestamp = await repository.GetLastestProcessedTimestamp();
            if (latestTimestamp == null)
            {
                log.LogInformation("No processed timestamp exists yet.");
                return;
            }

            var hashToPublish = latestTimestamp.ChainFingerprint;
            var hashAsString = hashToPublish.ToHexString();
            HttpResponseMessage response;

            var latestOriginStampPublished = await repository
                .GetLatestPublished("OriginStamp");

            if (latestOriginStampPublished != null &&
                latestOriginStampPublished.RowKey == latestTimestamp.RowKey)
            {
                log.LogInformation(
                    "Id {chainId} with hash {hash} in chain already published to OriginStamp.",
                    latestTimestamp.ChainId,
                    hashAsString);

                return;
            }

            using (var client = new HttpClient())
            {
                var url = config.GetValue<string>("TrustedTimestampProviders:OriginStamp:Url");
                var message = new HttpRequestMessage(HttpMethod.Post, url);
                message.Headers.Add(
                    "Authorization",
                    config.GetValue<string>("TrustedTimestampProviders:OriginStamp:ApiKey"));
                message.Content = new StringContent("{ \"hash\": \"" + hashAsString + "\" }");
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                message.Headers.Add("Accept", "application/json");
                try
                {
                    response = await client.SendAsync(message);
                    if (response.IsSuccessStatusCode)
                    {
                        await repository.SavePublished(
                            "OriginStamp",
                            latestTimestamp.RowKey,
                            latestTimestamp.ChainId,
                            hashAsString);

                        var verifiedHashes = await repository.GetPartOfChain(
                            latestOriginStampPublished == null ? 1 : latestOriginStampPublished.ChainId + 1,
                            latestTimestamp.ChainId);

                        log.LogInformation(
                            "Hashes verified by OriginStamp: {0}.",
                            verifiedHashes.Count);

                        foreach (var verifiedHash in verifiedHashes)
                        {
                            await repository.AddPublishedChainIdToHash(
                                verifiedHash.ItemFingerprint.ToHexString(),
                                latestTimestamp.ChainId);
                        }

                        await SendMail(verifiedHashes, latestOriginStampPublished, config, log);
                        return;
                    }
                    else
                    {
                        log.LogError(
                            "Publish to OriginStamp unsuccessful. Recieved status code {statusCode}",
                            response.StatusCode);
                        throw new Exception("Publish unsuccessful, see the log for more details.");
                    }
                }
                catch (Exception e)
                {
                    log.LogError("Exception thrown when publishing to OriginStamp: {e}", e);
                    throw;
                }
            }
        }

        private static async Task SendMail(
            ICollection<ProcessedTimestamp> verifiedHashes,
            PublishedTimestamp previouslyPublished,
            IConfigurationRoot config,
            ILogger log)
        {
            var now = DateTime.UtcNow;
            var timeOfDay = "morning";
            if (now.Hour > 12 && now.Hour < 18)
            {
                timeOfDay = "afternoon";
            }

            if (now.Hour >= 18)
            {
                timeOfDay = "evening";
            }

            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("Good " + timeOfDay + ",");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"The published value to OriginStamp is: {verifiedHashes.Last().ChainFingerprint.ToHexString()}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("The value was calculated as follows:");
            messageBuilder.AppendLine();
            if (previouslyPublished == null)
            {
                messageBuilder.AppendLine($"This is the first value to be published.");
            }
            else
            {
                messageBuilder.AppendLine($"Previous published value, at {previouslyPublished.Timestamp}, had ID {previouslyPublished.ChainId} and hash {previouslyPublished.PublishedHashString}.");
            }
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Chain Id | Hash | Hashed value | Chain hash");
            messageBuilder.AppendLine();
            foreach (var verifiedHash in verifiedHashes)
            {
                messageBuilder.AppendLine($"{verifiedHash.ChainId} | {verifiedHash.ItemFingerprint.ToHexString()} | {ShortFormat(verifiedHash.HashedValue.ToHexString())} | {ShortFormat(verifiedHash.ChainFingerprint.ToHexString())}");
                messageBuilder.AppendLine();
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine("The last chained hash was published to OriginStamp.");

            var client = new SendGridClient(config.GetValue<string>("SendGrid:ApiKey"));
            var to = new EmailAddress(config.GetValue<string>("SendGrid:ToMail"));
            var from = new EmailAddress(
                config.GetValue<string>("SendGrid:FromMail"),
                config.GetValue<string>("SendGrid:FromName"));

            var subject = "Successfully published to OriginStamp";
            var mail = MailHelper.CreateSingleEmail(from, to, subject, messageBuilder.ToString(), null);
            var response = await client.SendEmailAsync(mail);
            if ((int)response.StatusCode >= 400)
            {
                log.LogError(
                    "Failed sending mail through SendGrid. Status code is {statusCode}.",
                    response.StatusCode);
            }
        }

        /// <summary>
        /// Converts a hash to a short format, e.g. 9daf55cce3e231924ae0052602ffddd68adf9774 to 9daf...9774
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns></returns>
        private static string ShortFormat(string hash)
        {
            return hash.Substring(0, 4) + "..." + hash.Substring(hash.Length - 4, 4);
        }
    }
}
