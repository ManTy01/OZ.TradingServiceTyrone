using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeIngestion.Core.Snapshot
{
    public sealed class SnapshotResult
    {
        public bool Succeeded { get; init; }
        public int StatusCode { get; init; }
        public string? ErrorTitle { get; init; }
        public string? ErrorDetail { get; init; }
        public PortfolioSnapshot? Snapshot { get; init; }
        public static SnapshotResult Fail(int statusCode, string title, string detail) => new()
        {
            Succeeded = false,
            StatusCode = statusCode,
            ErrorTitle = title,
            ErrorDetail = detail
        };
        public static SnapshotResult Ok(PortfolioSnapshot snapshot) => new()
        {
            Succeeded = true,
            StatusCode = 200,
            Snapshot = snapshot
        };
    }
}
