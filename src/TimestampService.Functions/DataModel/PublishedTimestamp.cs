namespace TimestampService.Functions.DataModel
{
    using Microsoft.WindowsAzure.Storage.Table;

    public class PublishedTimestamp : TableEntity
    {
        public int ChainId { get; set; }

        public string PublishedHashString { get; set; }
    }
}
