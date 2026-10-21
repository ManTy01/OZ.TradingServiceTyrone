using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Entities
{
    /// <summary>A listed instrument. Currency is why FX exists (AAPL=USD, NPN=ZAR).</summary>
    public class Instrument
    {
        public int Id { get; set; }
        public required string Isin { get; set; }
        public required string Symbol { get; set; }
        /// <summary>ISO 4217 code of the instrument's price currency, e.g. USD or ZAR.</summary>
        public required string Currency { get; set; }
        public ICollection<MarketPrice> Prices { get; set; } = new List<MarketPrice>();
        public ICollection<TradeEvent> TradeEvents { get; set; } = new List<TradeEvent>();
    }
}
