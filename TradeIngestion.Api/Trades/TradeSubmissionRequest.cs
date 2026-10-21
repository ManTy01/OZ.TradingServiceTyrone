namespace TradeIngestion.Api.Trades
{
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
}
