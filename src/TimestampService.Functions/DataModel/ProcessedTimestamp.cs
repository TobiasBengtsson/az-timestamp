namespace TimestampService.Functions.DataModel
{
    using Microsoft.WindowsAzure.Storage.Table;
    using System;

    public class ProcessedTimestamp : TableEntity
    {
        public int ChainId { get; set; }

        public byte[] ItemFingerprint { get; set; }

        public byte[] HashedValue { get; set; }

        public byte[] ChainFingerprint { get; set; }

        public DateTimeOffset TimeAddedToQueue { get; set; }
    }
}
