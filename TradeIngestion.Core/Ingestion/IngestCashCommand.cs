using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Ingestion
{
    public sealed class IngestCashCommand
    {
        public required string ExternalRef { get; init; }
        public required string AccountCode { get; init; }
        public required string Type { get; init; }
        public decimal Amount { get; init; }
        public required string Currency { get; init; }
        public DateOnly TradeDate { get; init; }
        public DateTimeOffset AsOf { get; init; }
        public string? Description { get; init; }
    }
}
