using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Enums
{
    /// <summary>
    /// What we decided for one incoming message (ingest = bring data in).
    /// Stored on the row so logs, UI, and SQL can all show the same outcome.
    /// </summary>
    public enum IngestionDecision
    {
        Accepted = 1,
        DuplicateIgnored = 2,
        CorrectionApplied = 3
    }
}
