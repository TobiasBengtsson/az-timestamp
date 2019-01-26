namespace TimestampService.Functions.Dto
{
    using System.Collections.Generic;

    public class ValidationChainDto
    {
        public string Hash { get; set; }

        public bool IncludedInChain { get; set; }

        public bool Validated { get; set; }

        public int? ChainId { get; set; }

        public int? ValidatedChainId { get; set; }

        public string ValidatedHash { get; set; }

        public List<ValidationChainEntryDto> ValidationChain { get; set; }
    }
}
