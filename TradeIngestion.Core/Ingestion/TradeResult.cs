using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Enums;

namespace TradeIngestion.Core.Ingestion
{
    public sealed class TradeResult
    {
        public bool Succeeded { get; init; }
        public int StatusCode { get; init; }
        public string? ErrorTitle { get; init; }
        public string? ErrorDetail { get; init; }
        public Guid? EventId { get; init; }
        public string? ExternalRef { get; init; }
        public IngestionDecision? Decision { get; init; }
        public bool? IsApplied { get; init; }

        public static TradeResult Fail(int statusCode, string title, string detail) => new()
        {
            Succeeded = false,
            StatusCode = statusCode,
            ErrorTitle = title,
            ErrorDetail = detail
        };

        public static TradeResult Success(
            Guid eventId,
            string externalRef,
            IngestionDecision decision,
            bool isApplied,
            int statusCode) => new()
            {
                Succeeded = true,
                StatusCode = statusCode,
                EventId = eventId,
                ExternalRef = externalRef,
                Decision = decision,
                IsApplied = isApplied
            };
    }
}
