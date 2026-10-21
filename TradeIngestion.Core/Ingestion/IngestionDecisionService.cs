using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Enums;

namespace TradeIngestion.Core.Ingestion
{
    /// <summary>
    /// Decides whether an incoming message is new, a duplicate re-send, or a correction.
    /// Only <paramref name="appliedAsOfs"/> matter — ignored duplicates must not move the version clock.
    /// Time decides, not a field-by-field payload diff.
    /// </summary>
    public class IngestionDecisionService
    {
        /// <summary>
        /// New / duplicate / correction using only APPLIED asOf times (ignored duplicates must not move the clock).
        /// </summary>
        public IngestionDecision Decide(DateTimeOffset incomingAsOf,IReadOnlyCollection<DateTimeOffset> appliedAsOfs)
        {
            // First time we see this external_ref.
            if (appliedAsOfs.Count == 0)
            {
                return IngestionDecision.Accepted;
            }
            var latestAppliedAsOf = appliedAsOfs.Max();
            // Same or earlier asOf = re-send. Do not apply again.
            if (incomingAsOf <= latestAppliedAsOf)
            {
                return IngestionDecision.DuplicateIgnored;
            }
            // Later asOf = correction. New applied version; old rows stay.
            return IngestionDecision.CorrectionApplied;
        }

        /// <summary>True = this row counts in snapshots. Duplicates are stored but IsApplied is false.</summary>
        public bool IsApplied(IngestionDecision decision)
        {
            if (decision == IngestionDecision.Accepted)
            {
                return true;
            }
            if (decision == IngestionDecision.CorrectionApplied)
            {
                return true;
            }
            return false;
        }
    }
}
