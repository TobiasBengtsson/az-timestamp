namespace TimestampService.Functions.DataModel
{
    using Microsoft.WindowsAzure.Storage.Table;

    public class WaitingForProcessingTimestamp : TableEntity
    {
        public byte[] Fingerprint { get; set; }
    }
}
