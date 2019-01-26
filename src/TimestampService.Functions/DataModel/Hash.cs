using Microsoft.WindowsAzure.Storage.Table;

namespace TimestampService.Functions.DataModel
{
    public class Hash : TableEntity
    {
        public int? ChainId { get; set; }

        public int? PublishedOriginStampChainId { get; set; }
    }
}
