using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Entities;
using TradeIngestion.Core.Enums;
using TradeIngestion.Core.Ingestion;

namespace TradeIngestion.Infrastructure
{
    /// <summary>
    /// Incoming trades: validate → find account/instrument → duplicate vs correction → INSERT a new row.
    /// Never updates an old trade's quantity.
    /// </summary>
    public class TradeIngestionService : ITradeIngestionService
    {
        private readonly AppDbContext _db;
        private readonly IngestionDecisionService _decisions;
        private readonly ILogger<TradeIngestionService> _logger;
        public TradeIngestionService(
            AppDbContext db,
            IngestionDecisionService decisions,
            ILogger<TradeIngestionService> logger)
        {
            _db = db;
            _decisions = decisions;
            _logger = logger;
        }

        public async Task<TradeResult> IngestAsync(
            IngestTradeCommand command,
            bool enableDuplicateAndCorrectionHandling,
            CancellationToken cancellationToken)
        {
            var validationError = Validate(command);
            if (validationError is not null)
            {
                return TradeResult.Fail(400, "Validation failed", validationError);
            }

            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountCode == command.AccountCode, cancellationToken);
            if (account is null)
            {
                return TradeResult.Fail(
                    400,
                    "Unknown account",
                    $"No account exists with code '{command.AccountCode}'.");
            }

            var instrument = await _db.Instruments.FirstOrDefaultAsync(i => i.Isin == command.Isin, cancellationToken);
            if (instrument is null)
            {
                return TradeResult.Fail(
                    400,
                    "Unknown instrument",
                    $"No instrument exists with ISIN '{command.Isin}'.");
            }

            // Read-only lookup of prior messages for this ref. AsNoTracking = we will not update these rows.
            var existing = await _db.TradeEvents
                .AsNoTracking()
                .Where(e => e.ExternalRef == command.ExternalRef)
                .Select(e => new { e.AsOf, e.IsApplied })
                .ToListAsync(cancellationToken);
            IngestionDecision decision;
            // Flag OFF = legacy: reject if this ref already exists. Do not double-count.
            if (!enableDuplicateAndCorrectionHandling)
            {
                if (existing.Count > 0)
                {
                    _logger.LogWarning(
                        "Legacy ingest rejected duplicate {ExternalRef} {AccountCode} FeatureEnabled={FeatureEnabled}",
                        command.ExternalRef,
                        command.AccountCode,
                        false);
                    return TradeResult.Fail(
                        409,
                        "Duplicate trade",
                        $"A trade with external_ref '{command.ExternalRef}' already exists. Enable DuplicateAndCorrectionHandling to allow re-sends and corrections.");
                }
                decision = IngestionDecision.Accepted;
            }
            else
            {
                // Flag ON: only applied asOf values decide duplicate vs correction.
                var appliedAsOfs = existing
                    .Where(e => e.IsApplied)
                    .Select(e => e.AsOf)
                    .ToList();
                decision = _decisions.Decide(command.AsOf, appliedAsOfs);
            }
            var isApplied = _decisions.IsApplied(decision);
            var side = ParseSide(command.Side);

            if (side == TradeSide.Sell && isApplied)
            {
                var held = await GetHeldQuantityAsync(
                    account.Id,
                    instrument.Id,
                    command.TradeDate,
                    cancellationToken);
                if (command.Quantity > held)
                {
                    return TradeResult.Fail(
                        400,
                        "Insufficient shares",
                        $"Cannot sell {command.Quantity} {instrument.Symbol}. Account holds {held}.");
                }
            }

            var tradeEvent = new TradeEvent
            {
                Id = Guid.NewGuid(),
                ExternalRef = command.ExternalRef.Trim(),
                AccountId = account.Id,
                InstrumentId = instrument.Id,
                Side = side,
                Quantity = command.Quantity,
                Price = command.Price,
                Currency = instrument.Currency,
                TradeDate = command.TradeDate,
                AsOf = command.AsOf.ToUniversalTime(),
                ReceivedAt = DateTimeOffset.UtcNow,
                Decision = decision,
                IsApplied = isApplied
            };
            _db.TradeEvents.Add(tradeEvent);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Trade ingested {ExternalRef} {Decision} {AccountCode} {AsOf} {IsApplied} FeatureEnabled={FeatureEnabled}",
                tradeEvent.ExternalRef,
                tradeEvent.Decision,
                account.AccountCode,
                tradeEvent.AsOf,
                tradeEvent.IsApplied,
                enableDuplicateAndCorrectionHandling);
            var statusCode = decision == IngestionDecision.Accepted ? 201 : 200;
            return TradeResult.Success(
                tradeEvent.Id,
                tradeEvent.ExternalRef,
                tradeEvent.Decision,
                tradeEvent.IsApplied,
                statusCode);
        }

        /// <summary>
        /// Net applied quantity for this instrument as of the trade date
        /// (latest version per external_ref, same cutoff idea as the snapshot).
        /// </summary>
        private async Task<decimal> GetHeldQuantityAsync(
            int accountId,
            int instrumentId,
            DateOnly asOfDate,
            CancellationToken cancellationToken)
        {
            var cutoff = new DateTimeOffset(asOfDate.AddDays(1), TimeOnly.MinValue, TimeSpan.Zero);
            var rows = await _db.TradeEvents
                .AsNoTracking()
                .Where(e =>
                    e.AccountId == accountId
                    && e.InstrumentId == instrumentId
                    && e.IsApplied
                    && e.AsOf < cutoff
                    && e.TradeDate <= asOfDate)
                .Select(e => new { e.ExternalRef, e.Side, e.Quantity, e.AsOf, e.ReceivedAt })
                .ToListAsync(cancellationToken);

            var current = rows
                .GroupBy(e => e.ExternalRef)
                .Select(g => g.OrderByDescending(e => e.AsOf).ThenByDescending(e => e.ReceivedAt).First());

            decimal qty = 0;
            foreach (var trade in current)
            {
                qty += trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity;
            }

            return qty < 0 ? 0 : qty;
        }

        private static string? Validate(IngestTradeCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.ExternalRef))
            {
                return "externalRef is required.";
            }
            if (string.IsNullOrWhiteSpace(command.AccountCode))
            {
                return "accountId is required.";
            }
            if (string.IsNullOrWhiteSpace(command.Isin))
            {
                return "instrument.isin is required.";
            }
            if (ParseSide(command.Side) is 0 && !IsKnownSide(command.Side))
            {
                return "side must be BUY or SELL.";
            }
            if (command.Quantity <= 0)
            {
                return "quantity must be greater than 0.";
            }
            if (command.Price < 0)
            {
                return "price cannot be negative.";
            }
            if (command.AsOf == default)
            {
                return "asOf is required.";
            }
            if (command.TradeDate == default)
            {
                return "tradeDate is required.";
            }
            return null;
        }

        /// <summary>Is the JSON side a valid BUY/SELL (any casing)? Used for 400 validation.</summary>
        private static bool IsKnownSide(string side) =>
            side.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            || side.Equals("SELL", StringComparison.OrdinalIgnoreCase)
            || side.Equals("Buy", StringComparison.OrdinalIgnoreCase)
            || side.Equals("Sell", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// JSON sends "BUY" as text; the entity uses enum TradeSide.Buy.
        /// Ignore-case so "buy" and "BUY" both work. 0 = could not parse.
        /// </summary>
        private static TradeSide ParseSide(string side)
        {
            if (side.Equals("BUY", StringComparison.OrdinalIgnoreCase)
                || side.Equals("Buy", StringComparison.OrdinalIgnoreCase))
            {
                return TradeSide.Buy;
            }
            if (side.Equals("SELL", StringComparison.OrdinalIgnoreCase)
                || side.Equals("Sell", StringComparison.OrdinalIgnoreCase))
            {
                return TradeSide.Sell;
            }
            return 0;
        }
    }
}
