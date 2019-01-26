namespace TimestampService.Functions.Dto
{
    public class ValidationChainEntryDto
    {
        public int ChainId { get; set; }

        public string Hash { get; set; }

        public string HashedValue { get; set; }

        public string ChainHash { get; set; }
    }
}
