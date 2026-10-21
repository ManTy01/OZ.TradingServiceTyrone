namespace TradeIngestion.Api.Instruments;

public class InstrumentResponse
{
    public string Isin { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal? MarketPriceNative { get; set; }
    public decimal? MarketPriceUsd { get; set; }
}
