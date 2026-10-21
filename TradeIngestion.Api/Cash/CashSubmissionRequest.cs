namespace TradeIngestion.Api.Cash
{
    public class CashSubmissionRequest
    {
        public string ExternalRef { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateOnly TradeDate { get; set; }
        public DateTimeOffset AsOf { get; set; }
        public string? Description { get; set; }
    }
}
