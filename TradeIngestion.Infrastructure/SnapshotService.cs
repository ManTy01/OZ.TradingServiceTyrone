using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Entities;
using TradeIngestion.Core.Enums;
using TradeIngestion.Core.Snapshot;

namespace TradeIngestion.Infrastructure
{
    /// <summary>
    /// Builds the USD snapshot for Task 1 (HTTP). Same business rules as the SQL proc (Task 2).
    /// </summary>
    public class SnapshotService : ISnapshotService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SnapshotService> _logger;
        public SnapshotService(AppDbContext db, ILogger<SnapshotService> logger)
        {
            _db = db;
            _logger = logger;
        }
        public async Task<SnapshotResult> GetAsync(string accountCode, DateOnly asOfDate, CancellationToken cancellationToken)
        {
            // AsNoTracking: read-only. We will not update these rows.
            var account = await _db.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountCode == accountCode, cancellationToken);
            if (account is null)
            {
                return SnapshotResult.Fail(
                    404,
                    "Unknown account",
                    $"No account exists with code '{accountCode}'.");
            }
            // End of the snapshot day: 1 Mar includes asOf up to (not including) 2 Mar 00:00 UTC.
            var cutoff = new DateTimeOffset(asOfDate.AddDays(1), TimeOnly.MinValue, TimeSpan.Zero);
            var tradeRows = await _db.TradeEvents
                .AsNoTracking()
                .Where(e =>
                    e.AccountId == account.Id
                    && e.IsApplied
                    && e.AsOf < cutoff
                    && e.TradeDate <= asOfDate)
                .Select(e => new TradeRow
                {
                    ExternalRef = e.ExternalRef,
                    InstrumentId = e.InstrumentId,
                    Isin = e.Instrument.Isin,
                    Symbol = e.Instrument.Symbol,
                    InstrumentCurrency = e.Instrument.Currency,
                    Side = e.Side,
                    Quantity = e.Quantity,
                    Price = e.Price,
                    TradeDate = e.TradeDate,
                    AsOf = e.AsOf,
                    ReceivedAt = e.ReceivedAt
                })
                .ToListAsync(cancellationToken);
            var cashRows = await _db.CashEvents
                .AsNoTracking()
                .Where(e =>
                    e.AccountId == account.Id
                    && e.IsApplied
                    && e.AsOf < cutoff
                    && e.TradeDate <= asOfDate)
                .Select(e => new CashRow
                {
                    ExternalRef = e.ExternalRef,
                    Amount = e.Amount,
                    Currency = e.Currency,
                    AsOf = e.AsOf,
                    ReceivedAt = e.ReceivedAt
                })
                .ToListAsync(cancellationToken);
            // One winning version per external_ref: latest asOf (then ReceivedAt if tied).
            var currentTrades = tradeRows
                .GroupBy(t => t.ExternalRef)
                .Select(g => g
                    .OrderByDescending(t => t.AsOf)
                    .ThenByDescending(t => t.ReceivedAt)
                    .First())
                .ToList();
            var currentCash = cashRows
                .GroupBy(c => c.ExternalRef)
                .Select(g => g
                    .OrderByDescending(c => c.AsOf)
                    .ThenByDescending(c => c.ReceivedAt)
                    .First())
                .ToList();
            var positions = BuildPositions(currentTrades);
            decimal? usdZarRate = null;
            var needsZar = positions.Values.Any(p => p.Quantity != 0 && p.Currency == "ZAR") || currentCash.Any(c => c.Currency == "ZAR");
            if (needsZar)
            {
                var fx = await _db.FxRates
                    .AsNoTracking()
                    .Where(r =>
                        r.BaseCurrency == "USD"
                        && r.QuoteCurrency == "ZAR"
                        && r.AsOfDate <= asOfDate)
                    .OrderByDescending(r => r.AsOfDate)
                    .FirstOrDefaultAsync(cancellationToken);
                if (fx is null)
                {
                    return SnapshotResult.Fail(
                        400,
                        "Missing FX rate",
                        $"No USD-ZAR rate on or before {asOfDate:yyyy-MM-dd}.");
                }
                usdZarRate = fx.Rate; // 1 USD = this many ZAR. Convert ZAR → USD by dividing.
            }
            var instrumentIds = positions.Keys.ToList();
            var priceRows = instrumentIds.Count == 0
                ? []
                : await _db.MarketPrices
                    .AsNoTracking()
                    .Where(p => instrumentIds.Contains(p.InstrumentId) && p.AsOfDate <= asOfDate)
                    .ToListAsync(cancellationToken);
            var latestPrices = priceRows
                .GroupBy(p => p.InstrumentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(p => p.AsOfDate).First());
            var positionLines = new List<PositionLine>();
            foreach (var (instrumentId, state) in positions.OrderBy(p => p.Value.Symbol))
            {
                if (state.Quantity == 0)
                {
                    continue;
                }
                if (!latestPrices.TryGetValue(instrumentId, out var price))
                {
                    return SnapshotResult.Fail(
                        400,
                        "Missing market price",
                        $"No market price for {state.Symbol} on or before {asOfDate:yyyy-MM-dd}.");
                }
                if (!TryConvertToUsd(state.Quantity == 0 ? 0 : state.CostPool / state.Quantity, state.Currency, usdZarRate, out var unitCostUsd, out var fxError)
                    || !TryConvertToUsd(price.Price, price.Currency, usdZarRate, out var marketPriceUsd, out fxError))
                {
                    return SnapshotResult.Fail(400, "FX conversion failed", fxError!);
                }
                var marketValueUsd = state.Quantity * marketPriceUsd;
                var costBasisUsd = state.Quantity * unitCostUsd;
                positionLines.Add(new PositionLine
                {
                    Isin = state.Isin,
                    Symbol = state.Symbol,
                    Quantity = RoundQty(state.Quantity),
                    UnitCost = RoundMoney(unitCostUsd),
                    MarketPriceNative = RoundMoney(price.Price),
                    InstrumentCurrency = price.Currency,
                    MarketPrice = RoundMoney(marketPriceUsd),
                    MarketValue = RoundMoney(marketValueUsd),
                    UnrealizedPnl = RoundMoney(marketValueUsd - costBasisUsd)
                });
            }
            var cashLines = new List<CashLine>();
            foreach (var group in currentCash.GroupBy(c => c.Currency).OrderBy(g => g.Key))
            {
                var amount = group.Sum(c => c.Amount);
                if (!TryConvertToUsd(amount, group.Key, usdZarRate, out var amountUsd, out var fxError))
                {
                    return SnapshotResult.Fail(400, "FX conversion failed", fxError!);
                }
                cashLines.Add(new CashLine
                {
                    Currency = group.Key,
                    Amount = RoundMoney(amount),
                    AmountUsd = RoundMoney(amountUsd)
                });
            }
            var totalMarketValue = positionLines.Sum(p => p.MarketValue);
            var cashTotalUsd = cashLines.Sum(c => c.AmountUsd);
            var snapshot = new PortfolioSnapshot
            {
                AccountId = account.AccountCode,
                AsOfDate = asOfDate,
                Currency = "USD",
                Positions = positionLines,
                Cash = cashLines,
                CashTotalUsd = RoundMoney(cashTotalUsd),
                TotalMarketValue = RoundMoney(totalMarketValue),
                TotalValue = RoundMoney(totalMarketValue + cashTotalUsd),
                FxRateUsdZar = usdZarRate
            };
            _logger.LogInformation(
                "Snapshot produced {AccountCode} {Date} {PositionCount} {TotalMarketValue} {TotalValue}",
                account.AccountCode,
                asOfDate,
                positionLines.Count,
                snapshot.TotalMarketValue,
                snapshot.TotalValue);
            return SnapshotResult.Ok(snapshot);
        }
        /// <summary>
        /// Running average cost per instrument.
        /// Dictionary key = InstrumentId (one pot for AAPL, one for NPN).
        /// Must walk trades in time order so a sell uses the average from just before that sell.
        /// </summary>
        private static Dictionary<int, PositionState> BuildPositions(IEnumerable<TradeRow> currentTrades)
        {
            var positions = new Dictionary<int, PositionState>();
            foreach (var trade in currentTrades
                .OrderBy(t => t.TradeDate)
                .ThenBy(t => t.AsOf)
                .ThenBy(t => t.ReceivedAt))
            {
                if (!positions.TryGetValue(trade.InstrumentId, out var state))
                {
                    state = new PositionState
                    {
                        Isin = trade.Isin,
                        Symbol = trade.Symbol,
                        Currency = trade.InstrumentCurrency
                    };
                    positions[trade.InstrumentId] = state;
                }
                if (trade.Side == TradeSide.Buy)
                {
                    state.Quantity += trade.Quantity;
                    state.CostPool += trade.Quantity * trade.Price;
                }
                else
                {
                    // Sell: remove shares at the current average. Remaining shares keep that average.
                    if (state.Quantity > 0)
                    {
                        var averageCost = state.CostPool / state.Quantity;
                        var covered = Math.Min(trade.Quantity, state.Quantity);
                        state.CostPool -= covered * averageCost;
                    }
                    state.Quantity -= trade.Quantity;
                    if (state.Quantity <= 0)
                    {
                        state.CostPool = 0;
                    }
                }
            }
            return positions;
        }
        /// <summary>USD stays as-is. ZAR is divided by the rate (1 USD = rate ZAR). Never multiply.</summary>
        private static bool TryConvertToUsd(decimal amount, string currency, decimal? usdZarRate, out decimal usd, out string? error)
        {
            error = null;
            if (currency == "USD")
            {
                usd = amount;
                return true;
            }
            if (currency == "ZAR")
            {
                if (usdZarRate is null or 0)
                {
                    usd = 0;
                    error = "USD-ZAR rate is missing or zero; cannot convert ZAR to USD. Rate is Quote per 1 Base (ZAR per USD); divide ZAR by the rate.";
                    return false;
                }
                usd = amount / usdZarRate.Value;
                return true;
            }
            usd = 0;
            error = $"Unsupported currency '{currency}'.";
            return false;
        }

        private static decimal RoundMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static decimal RoundQty(decimal value) =>
            Math.Round(value, 6, MidpointRounding.AwayFromZero);

        /// <summary>Helper bag of columns for the snapshot query (not a full EF entity).</summary>
        private sealed class TradeRow
        {
            public string ExternalRef { get; set; } = "";
            public int InstrumentId { get; set; }
            public string Isin { get; set; } = "";
            public string Symbol { get; set; } = "";
            public string InstrumentCurrency { get; set; } = "";
            public TradeSide Side { get; set; }
            public decimal Quantity { get; set; }
            public decimal Price { get; set; }
            public DateOnly TradeDate { get; set; }
            public DateTimeOffset AsOf { get; set; }
            public DateTimeOffset ReceivedAt { get; set; }
        }

        /// <summary>Helper bag of cash columns for the snapshot query.</summary>
        private sealed class CashRow
        {
            public string ExternalRef { get; set; } = "";
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "";
            public DateTimeOffset AsOf { get; set; }
            public DateTimeOffset ReceivedAt { get; set; }
        }

        /// <summary>One running position: shares + cost pot. Unit cost = CostPool / Quantity.</summary>
        private sealed class PositionState
        {
            public required string Isin { get; init; }
            public required string Symbol { get; init; }
            public required string Currency { get; init; }
            public decimal Quantity { get; set; }
            public decimal CostPool { get; set; }
        }
    }

}
