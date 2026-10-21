using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Entities
{
    /// <summary>Close price for one instrument on one calendar day. Unique (InstrumentId, AsOfDate).</summary>
    public class MarketPrice
    {
        public int Id { get; set; }
        public int InstrumentId { get; set; }
        public Instrument Instrument { get; set; } = null!;
        public DateOnly AsOfDate { get; set; }
        public decimal Price { get; set; }
        public required string Currency { get; set; }
    }
}
