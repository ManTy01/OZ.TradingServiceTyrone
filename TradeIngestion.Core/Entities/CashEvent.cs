using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Enums;

namespace TradeIngestion.Core.Entities
{
    /// <summary>
    /// Incoming cash message. Same as-of duplicate/correction rules as trades, no instrument.
    /// Amount is signed: fees must be negative.
    /// </summary>
    public class CashEvent
    {
        public Guid Id { get; set; }
        public required string ExternalRef { get; set; }
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;
        public CashType Type { get; set; }
        public decimal Amount { get; set; }
        public required string Currency { get; set; }
        public DateOnly TradeDate { get; set; }
        public DateTimeOffset AsOf { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public IngestionDecision Decision { get; set; }
        public bool IsApplied { get; set; }
        public string? Description { get; set; }
    }
}
