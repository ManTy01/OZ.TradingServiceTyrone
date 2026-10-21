using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Enums
{
    /// <summary>Fee must be a negative amount. CashAdjustment can be + or −.</summary>
    public enum CashType
    {
        Fee = 1,
        CashAdjustment = 2
    }
}
