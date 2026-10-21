using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Enums
{
    /// <summary>Buy adds quantity; Sell subtracts. JSON sends "BUY"/"SELL"; we map to this enum.</summary>
    public enum TradeSide
    {
        Buy = 1,
        Sell = 2
    }
}
