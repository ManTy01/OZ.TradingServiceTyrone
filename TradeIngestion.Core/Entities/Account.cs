using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Entities
{
    /// <summary>An account in the book. AccountCode (ACC-001) is what the API uses; Id is the internal PK.</summary>
    public class Account
    {
        public int Id { get; set; }
        /// <summary>Business key from upstream, e.g. ACC-001. Unique in SQL.</summary>
        public required string AccountCode { get; set; }
        public required string Name { get; set; }
        public ICollection<TradeEvent> TradeEvents { get; set; } = new List<TradeEvent>();
        public ICollection<CashEvent> CashEvents { get; set; } = new List<CashEvent>();
    }
}
