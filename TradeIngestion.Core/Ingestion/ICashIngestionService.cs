using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Ingestion
{
    /// <summary>Job description: accept incoming cash. Implemented by CashIngestionService.</summary>
    public interface ICashIngestionService
    {
        Task<TradeResult> IngestAsync(
            IngestCashCommand command,
            bool enableDuplicateAndCorrectionHandling,
            CancellationToken cancellationToken);
    }
}
