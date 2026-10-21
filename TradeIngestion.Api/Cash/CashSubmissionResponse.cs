using TradeIngestion.Core.Enums;

namespace TradeIngestion.Api.Cash
{
    public class CashSubmissionResponse
    {
        public Guid Id { get; set; }
        public string ExternalRef { get; set; } = string.Empty;
        public IngestionDecision Decision { get; set; }
        public bool IsApplied { get; set; }
    }
}
