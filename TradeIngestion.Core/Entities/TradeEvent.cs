using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Enums;

namespace TradeIngestion.Core.Entities
{
    /// <summary>
    /// One incoming trade message (ingest = bring data in).
    /// We never UPDATE quantity/price on an old row — a correction is a NEW row.
    /// ExternalRef is NOT unique: original + duplicate + correction are three rows.
    /// </summary>
    public class TradeEvent
    {
        public Guid Id { get; set; }
        /// <summary>Upstream id, e.g. TRX-1001. Same value can appear on many rows.</summary>
        public required string ExternalRef { get; set; }
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;
        public int InstrumentId { get; set; }
        public Instrument Instrument { get; set; } = null!;
        public TradeSide Side { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public required string Currency { get; set; }
        public DateOnly TradeDate { get; set; }
        /// <summary>Upstream version clock (UTC). Decides duplicate vs correction.</summary>
        public DateTimeOffset AsOf { get; set; }
        /// <summary>When this service persisted the row. Not used for versioning.</summary>
        public DateTimeOffset ReceivedAt { get; set; }
        public IngestionDecision Decision { get; set; }
        /// <summary>Snapshot and SQL proc read only applied rows.</summary>
        public bool IsApplied { get; set; }
    }
}
