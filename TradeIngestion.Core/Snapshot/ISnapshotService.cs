using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Snapshot
{
    /// <summary>
    /// Job description: USD portfolio report for an account on a date.
    /// Implemented by SnapshotService. Blazor/Swagger call this through the API, not directly.
    /// </summary>
    public interface ISnapshotService
    {
        Task<SnapshotResult> GetAsync(
            string accountCode,
            DateOnly asOfDate,
            CancellationToken cancellationToken);
    }
}
