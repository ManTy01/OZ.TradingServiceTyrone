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
    /// <summary>Incoming cash. Same as-of duplicate/correction rules as trades, different table.</summary>
    public class CashIngestionService : ICashIngestionService
    {
        private readonly AppDbContext _db;
        private readonly IngestionDecisionService _decisions;
        private readonly ILogger<CashIngestionService> _logger;
        public CashIngestionService(AppDbContext db, IngestionDecisionService decisions, ILogger<CashIngestionService> logger)
        {
            _db = db;
            _decisions = decisions;
            _logger = logger;
        }
        public async Task<TradeResult> IngestAsync(IngestCashCommand command, bool enableDuplicateAndCorrectionHandling, CancellationToken cancellationToken)
        {
            var validationError = Validate(command);
            if (validationError is not null)
            {
                return TradeResult.Fail(400, "Validation failed", validationError);
            }
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.AccountCode == command.AccountCode, cancellationToken);
            if (account is null)
            {
                return TradeResult.Fail(
                    400,
                    "Unknown account",
                    $"No account exists with code '{command.AccountCode}'.");
            }
            var existing = await _db.CashEvents
                .AsNoTracking()
                .Where(e => e.ExternalRef == command.ExternalRef)
                .Select(e => new { e.AsOf, e.IsApplied })
                .ToListAsync(cancellationToken);
            IngestionDecision decision;
            if (!enableDuplicateAndCorrectionHandling)
            {
                if (existing.Count > 0)
                {
                    _logger.LogWarning(
                        "Legacy cash ingest rejected duplicate {ExternalRef} {AccountCode} FeatureEnabled={FeatureEnabled}",
                        command.ExternalRef,
                        command.AccountCode,
                        false);
                    return TradeResult.Fail(
                        409,
                        "Duplicate cash event",
                        $"A cash event with external_ref '{command.ExternalRef}' already exists. Enable DuplicateAndCorrectionHandling to allow re-sends and corrections.");
                }
                decision = IngestionDecision.Accepted;
            }
            else
            {
                var appliedAsOfs = existing
                    .Where(e => e.IsApplied)
                    .Select(e => e.AsOf)
                    .ToList();
                decision = _decisions.Decide(command.AsOf, appliedAsOfs);
            }
            var isApplied = _decisions.IsApplied(decision);
            var type = ParseType(command.Type);
            var cashEvent = new CashEvent
            {
                Id = Guid.NewGuid(),
                ExternalRef = command.ExternalRef.Trim(),
                AccountId = account.Id,
                Type = type,
                Amount = command.Amount,
                Currency = command.Currency.Trim().ToUpperInvariant(),
                TradeDate = command.TradeDate,
                AsOf = command.AsOf.ToUniversalTime(),
                ReceivedAt = DateTimeOffset.UtcNow,
                Decision = decision,
                IsApplied = isApplied,
                Description = string.IsNullOrWhiteSpace(command.Description)
                    ? null
                    : command.Description.Trim()
            };
            _db.CashEvents.Add(cashEvent);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Cash ingested {ExternalRef} {Decision} {AccountCode} {Type} {Amount} {Currency} {AsOf} {IsApplied} FeatureEnabled={FeatureEnabled}",
                cashEvent.ExternalRef,
                cashEvent.Decision,
                account.AccountCode,
                cashEvent.Type,
                cashEvent.Amount,
                cashEvent.Currency,
                cashEvent.AsOf,
                cashEvent.IsApplied,
                enableDuplicateAndCorrectionHandling);
            var statusCode = decision == IngestionDecision.Accepted ? 201 : 200;
            return TradeResult.Success(
                cashEvent.Id,
                cashEvent.ExternalRef,
                cashEvent.Decision,
                cashEvent.IsApplied,
                statusCode);
        }
        private static string? Validate(IngestCashCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.ExternalRef))
            {
                return "externalRef is required.";
            }
            if (string.IsNullOrWhiteSpace(command.AccountCode))
            {
                return "accountId is required.";
            }
            if (!TryParseType(command.Type, out var type))
            {
                return "type must be FEE or CASH_ADJUSTMENT.";
            }
            if (command.Amount == 0)
            {
                return "amount cannot be 0.";
            }
            if (type == CashType.Fee && command.Amount > 0)
            {
                return "FEE amount must be negative.";
            }
            if (string.IsNullOrWhiteSpace(command.Currency) || command.Currency.Trim().Length != 3)
            {
                return "currency must be a 3-letter ISO code.";
            }
            var currency = command.Currency.Trim().ToUpperInvariant();
            if (currency is not "USD" and not "ZAR")
            {
                return "currency must be USD or ZAR for this service.";
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
        private static bool TryParseType(string value, out CashType type)
        {
            if (value.Equals("FEE", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Fee", StringComparison.OrdinalIgnoreCase))
            {
                type = CashType.Fee;
                return true;
            }
            if (value.Equals("CASH_ADJUSTMENT", StringComparison.OrdinalIgnoreCase)
                || value.Equals("CashAdjustment", StringComparison.OrdinalIgnoreCase))
            {
                type = CashType.CashAdjustment;
                return true;
            }
            type = default;
            return false;
        }
        private static CashType ParseType(string value)
        {
            TryParseType(value, out var type);
            return type;
        }
    }
}
