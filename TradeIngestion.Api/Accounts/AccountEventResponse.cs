using TradeIngestion.Core.Enums;

namespace TradeIngestion.Api.Accounts
{
    public class AccountEventResponse
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string ExternalRef { get; set; } = string.Empty;
        public DateOnly TradeDate { get; set; }
        public DateTimeOffset AsOf { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public IngestionDecision Decision { get; set; }
        public bool IsApplied { get; set; }
        public string? InstrumentIsin { get; set; }
        public string? InstrumentSymbol { get; set; }
        public string? Side { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Price { get; set; }
        public string? CashType { get; set; }
        public decimal? Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
