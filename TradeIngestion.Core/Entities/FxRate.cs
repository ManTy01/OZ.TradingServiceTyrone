using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Entities
{
    /// <summary>
    /// Daily FX rate. Rate is Quote per 1 Base.
    /// Example: Base=USD, Quote=ZAR, Rate=18.72 means 1 USD = 18.72 ZAR.
    /// Convert ZAR → USD with: usd = zarAmount / Rate.
    /// </summary>
    public class FxRate
    {
        public int Id { get; set; }
        public required string BaseCurrency { get; set; }
        public required string QuoteCurrency { get; set; }
        public DateOnly AsOfDate { get; set; }
        public decimal Rate { get; set; }
    }
}
