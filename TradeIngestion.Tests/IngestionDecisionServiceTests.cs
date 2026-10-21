using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Enums;
using TradeIngestion.Core.Ingestion;

namespace TradeIngestion.Tests
{
    /// <summary>No database. Proves new / duplicate / correction from asOf only.</summary>
    public class IngestionDecisionServiceTests
    {
        private readonly IngestionDecisionService _sut = new();
        private static readonly DateTimeOffset OriginalAsOf =
            DateTimeOffset.Parse("2025-03-01T10:15:00Z");
        private static readonly DateTimeOffset SameDayLaterAsOf =
            DateTimeOffset.Parse("2025-03-01T16:00:00Z");
        private static readonly DateTimeOffset NextDayAsOf =
            DateTimeOffset.Parse("2025-03-02T09:00:00Z");
        private static readonly DateTimeOffset EarlierRetryAsOf =
            DateTimeOffset.Parse("2025-03-01T09:00:00Z");

        [Fact]
        public void Decide_NoAppliedEvents_ReturnsAccepted()
        {
            var decision = _sut.Decide(OriginalAsOf, []);
            Assert.Equal(IngestionDecision.Accepted, decision);
            Assert.True(_sut.IsApplied(decision));
        }

        [Fact]
        public void Decide_SameAsOfAsLatestApplied_ReturnsDuplicateIgnored()
        {
            var decision = _sut.Decide(OriginalAsOf, [OriginalAsOf]);
            Assert.Equal(IngestionDecision.DuplicateIgnored, decision);
            Assert.False(_sut.IsApplied(decision));
        }

        [Fact]
        public void Decide_EarlierAsOfThanLatestApplied_ReturnsDuplicateIgnored()
        {
            var decision = _sut.Decide(EarlierRetryAsOf, [OriginalAsOf]);
            Assert.Equal(IngestionDecision.DuplicateIgnored, decision);
            Assert.False(_sut.IsApplied(decision));
        }
        [Fact]

        public void Decide_LaterAsOfThanLatestApplied_ReturnsCorrectionApplied()
        {
            var decision = _sut.Decide(NextDayAsOf, [OriginalAsOf]);
            Assert.Equal(IngestionDecision.CorrectionApplied, decision);
            Assert.True(_sut.IsApplied(decision));
        }

        [Fact]
        public void Decide_UsesMaximumAppliedAsOf_NotTheFirst()
        {
            var applied = new[] { OriginalAsOf, NextDayAsOf };
            var lateRetryOfFirstVersion = _sut.Decide(SameDayLaterAsOf, applied);
            Assert.Equal(IngestionDecision.DuplicateIgnored, lateRetryOfFirstVersion);
        }
    }
}
