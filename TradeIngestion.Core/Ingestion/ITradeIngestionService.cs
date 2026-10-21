using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Ingestion
{
    /// <summary>
    /// Job description: accept an incoming trade.
    /// Implemented by TradeIngestionService in Infrastructure. Wired in Program.cs.
    /// </summary>
    public interface ITradeIngestionService
    {
        Task<TradeResult> IngestAsync(
            IngestTradeCommand command,
            bool enableDuplicateAndCorrectionHandling,
            CancellationToken cancellationToken);
    }
}
