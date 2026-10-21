using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Ingestion
{
    public sealed class IngestTradeCommand
    {
        public required string ExternalRef { get; init; }
        public required string AccountCode { get; init; }
        public required string Isin { get; init; }
        public required string Side { get; init; }
        public decimal Quantity { get; init; }
        public decimal Price { get; init; }
        public DateOnly TradeDate { get; init; }
        public DateTimeOffset AsOf { get; init; }
    }
}
