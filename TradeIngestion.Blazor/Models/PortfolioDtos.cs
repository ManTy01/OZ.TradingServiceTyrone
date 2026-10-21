namespace TradeIngestion.Blazor.Models
{
    public class InstrumentSummary
    {
        public string Isin { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal? MarketPriceNative { get; set; }
        public decimal? MarketPriceUsd { get; set; }
    }

    public class TradeSubmissionRequest
    {
        public string ExternalRef { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public InstrumentRefRequest Instrument { get; set; } = new();
        public string Side { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateOnly TradeDate { get; set; }
        public DateTimeOffset AsOf { get; set; }
    }

    public class InstrumentRefRequest
    {
        public string Isin { get; set; } = string.Empty;
        public string? Symbol { get; set; }
    }

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

    public class AccountSummary
    {
        public string AccountCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
    public class PortfolioSnapshot
    {
        public string AccountId { get; set; } = string.Empty;
        public DateOnly AsOfDate { get; set; }
        public string Currency { get; set; } = "USD";
        public List<PositionLine> Positions { get; set; } = [];
        public List<CashLine> Cash { get; set; } = [];
        public decimal CashTotalUsd { get; set; }
        public decimal TotalMarketValue { get; set; }
        public decimal TotalValue { get; set; }
        public decimal? FxRateUsdZar { get; set; }
    }
    public class PositionLine
    {
        public string Isin { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal MarketPriceNative { get; set; }
        public string InstrumentCurrency { get; set; } = string.Empty;
        public decimal MarketPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal UnrealizedPnl { get; set; }
    }
    public class CashLine
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal AmountUsd { get; set; }
    }
    public class AccountEvent
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string ExternalRef { get; set; } = string.Empty;
        public DateOnly TradeDate { get; set; }
        public DateTimeOffset AsOf { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public string Decision { get; set; } = string.Empty;
        public bool IsApplied { get; set; }
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
