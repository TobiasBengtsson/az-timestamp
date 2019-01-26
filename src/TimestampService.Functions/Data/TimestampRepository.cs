using Microsoft.WindowsAzure.Storage.Table;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimestampService.Functions.DataModel;
using TimestampService.Functions.Extensions;

namespace TimestampService.Functions.Data
{
    internal class TimestampRepository
    {
        private readonly CloudTable table;

        public TimestampRepository(CloudTable table)
        {
            this.table = table;
        }

        public async Task AddChainIdToHash(string hash, int chainId)
        {
            await table.ExecuteAsync(TableOperation.InsertOrMerge(new Hash { PartitionKey = "Hash", RowKey = hash.ToUpper(), ChainId = chainId }));
        }

        public async Task AddPublishedChainIdToHash(string hash, int chainId)
        {
            await table.ExecuteAsync(TableOperation.InsertOrMerge(new Hash { PartitionKey = "Hash", RowKey = hash.ToUpper(), PublishedOriginStampChainId = chainId }));
        }

        public async Task AddHash(string hash)
        {
            await table.ExecuteAsync(TableOperation.Insert(new TableEntity { PartitionKey = "Hash", RowKey = hash.ToUpper() }));
        }

        public async Task AddProcessedTimestamp(ProcessedTimestamp processedTimestamp)
        {
            await table.ExecuteAsync(TableOperation.Insert(processedTimestamp));
        }

        public async Task AddWaitForProcessing(WaitingForProcessingTimestamp waitingForProcessingTimestamp)
        {
            await table.ExecuteAsync(TableOperation.Insert(waitingForProcessingTimestamp));
        }

        public async Task<IEnumerable<WaitingForProcessingTimestamp>> GetAllWaitingForProcessing()
        {
            var query = new TableQuery<WaitingForProcessingTimestamp>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "WaitingForProcessing"));

            return await table.ExecuteQueryAsync(query);
        }

        public async Task<Hash> GetHash(string hash)
        {
            var tableResult = await table.ExecuteAsync(TableOperation.Retrieve<Hash>("Hash", hash.ToUpper()));
            return (Hash)tableResult.Result;
        }

        public async Task<ProcessedTimestamp> GetLastestProcessedTimestamp()
        {
            var query = new TableQuery<ProcessedTimestamp>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "ProcessedTimestamp")).Take(1);

            return (await table.ExecuteQueryAsync(query)).FirstOrDefault();
        }

        public async Task<PublishedTimestamp> GetLatestPublished(string provider)
        {
            var query = new TableQuery<PublishedTimestamp>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, GetPublishedPartitionKey(provider))).Take(1);

            return (await table.ExecuteQueryAsync(query)).FirstOrDefault();
        }

        public async Task<ICollection<ProcessedTimestamp>> GetPartOfChain(int fromChainId, int toChainId)
        {
            var partitionFilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "ProcessedTimestamp");
            var gtFilter = TableQuery.GenerateFilterConditionForInt("ChainId", QueryComparisons.GreaterThanOrEqual, fromChainId);
            var ltFilter = TableQuery.GenerateFilterConditionForInt("ChainId", QueryComparisons.LessThanOrEqual, toChainId);
            var chainIdFilter = TableQuery.CombineFilters(gtFilter, TableOperators.And, ltFilter);
            var finalFilter = TableQuery.CombineFilters(partitionFilter, TableOperators.And, chainIdFilter);

            var query = new TableQuery<ProcessedTimestamp>()
                .Where(finalFilter);

            var chain = await table.ExecuteQueryAsync(query);
            return chain.OrderBy(ch => ch.ChainId).ToList();
        }

        public async Task<bool> HashExists(string hash)
        {
            var result = await table.ExecuteAsync(TableOperation.Retrieve<TableEntity>("Hash", hash.ToUpper()));
            return result.Result != null;
        }

        public async Task RemoveWaitingForProcessingTimestamp(WaitingForProcessingTimestamp itemToRemove)
        {
            await table.ExecuteAsync(TableOperation.Delete(itemToRemove));
        }

        public async Task SavePublished(string provider, string rowKey, int chainId, string hashAsString)
        {
            var entity = new PublishedTimestamp
            {
                PartitionKey = GetPublishedPartitionKey(provider),
                RowKey = rowKey,
                ChainId = chainId,
                PublishedHashString = hashAsString.ToUpper()
            };

            await table.ExecuteAsync(TableOperation.Insert(entity));
        }
        
        private string GetPublishedPartitionKey(string provider)
        {
            return "Published" + provider;
        }
    }
}
