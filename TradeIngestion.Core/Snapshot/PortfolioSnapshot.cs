using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Snapshot
{
    public sealed class PortfolioSnapshot
    {
        public required string AccountId { get; init; }
        public DateOnly AsOfDate { get; init; }
        public string Currency { get; init; } = "USD";
        public IReadOnlyList<PositionLine> Positions { get; init; } = [];
        public IReadOnlyList<CashLine> Cash { get; init; } = [];
        public decimal CashTotalUsd { get; init; }
        public decimal TotalMarketValue { get; init; }
        public decimal TotalValue { get; init; }
        public decimal? FxRateUsdZar { get; init; }
    }

    public sealed class PositionLine
    {
        public required string Isin { get; init; }
        public required string Symbol { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public decimal MarketPriceNative { get; init; }
        public required string InstrumentCurrency { get; init; }
        public decimal MarketPrice { get; init; }
        public decimal MarketValue { get; init; }
        public decimal UnrealizedPnl { get; init; }
    }
    public sealed class CashLine
    {
        public required string Currency { get; init; }
        public decimal Amount { get; init; }
        public decimal AmountUsd { get; init; }
    }
}
