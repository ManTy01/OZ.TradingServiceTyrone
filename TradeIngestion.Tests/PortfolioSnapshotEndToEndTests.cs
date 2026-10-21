using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TradeIngestion.Api.Cash;
using TradeIngestion.Api.Trades;
using TradeIngestion.Core.Enums;
using TradeIngestion.Core.Snapshot;

namespace TradeIngestion.Tests
{
    /// <summary>
    /// Real HTTP against LocalDB: POST trade, duplicate, correction, cash, then snapshots.
    /// Uses ACC-002 and 15–16 Oct 2026 so ACC-001 (15–16 Oct) demo data is not mixed in.
    /// </summary>
    public class PortfolioSnapshotEndToEndTests : IClassFixture<TradeIngestionWebAppFactory>, IAsyncLifetime
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        private readonly TradeIngestionWebAppFactory _factory;
        private readonly HttpClient _client;
        public PortfolioSnapshotEndToEndTests(TradeIngestionWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }
        public Task InitializeAsync() => _factory.ResetLedgerAsync();
        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SellMoreThanHeld_Returns400()
        {
            var sell = await _client.PostAsJsonAsync("/api/trades", new TradeSubmissionRequest
            {
                ExternalRef = "E2E-TRX-SELL-OVER",
                AccountId = "ACC-002",
                Instrument = new InstrumentRefRequest
                {
                    Isin = "US0378331005",
                    Symbol = "AAPL"
                },
                Side = "SELL",
                Quantity = 1m,
                Price = 186m,
                TradeDate = new DateOnly(2026, 10, 15),
                AsOf = DateTimeOffset.Parse("2026-10-15T12:00:00Z")
            });

            Assert.Equal(HttpStatusCode.BadRequest, sell.StatusCode);
            var body = await sell.Content.ReadAsStringAsync();
            Assert.Contains("Insufficient shares", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task IngestDuplicateCorrectionAndCash_SnapshotsRespectAsOf()
        {
            var original = await PostTradeAsync(new TradeSubmissionRequest
            {
                ExternalRef = "E2E-TRX-1001",
                AccountId = "ACC-002",
                Instrument = new InstrumentRefRequest
                {
                    Isin = "US0378331005",
                    Symbol = "AAPL"
                },
                Side = "BUY",
                Quantity = 120m,
                Price = 185.40m,
                TradeDate = new DateOnly(2026, 10, 15),
                AsOf = DateTimeOffset.Parse("2026-10-15T10:15:00Z")
            });
            Assert.Equal(HttpStatusCode.Created, original.Status);
            Assert.Equal(IngestionDecision.Accepted, original.Body!.Decision);
            Assert.True(original.Body.IsApplied);
            var duplicate = await PostTradeAsync(new TradeSubmissionRequest
            {
                ExternalRef = "E2E-TRX-1001",
                AccountId = "ACC-002",
                Instrument = new InstrumentRefRequest
                {
                    Isin = "US0378331005",
                    Symbol = "AAPL"
                },
                Side = "BUY",
                Quantity = 120m,
                Price = 185.40m,
                TradeDate = new DateOnly(2026, 10, 15),
                AsOf = DateTimeOffset.Parse("2026-10-15T10:15:00Z")
            });
            Assert.Equal(HttpStatusCode.OK, duplicate.Status);
            Assert.Equal(IngestionDecision.DuplicateIgnored, duplicate.Body!.Decision);
            Assert.False(duplicate.Body.IsApplied);
            var fifteenth = await GetSnapshotAsync("ACC-002", "2026-10-15");
            var aaplFifteenth = Assert.Single(fifteenth.Positions);
            Assert.Equal(120m, aaplFifteenth.Quantity);
            Assert.Equal(22320.00m, aaplFifteenth.MarketValue);
            Assert.Equal(72.00m, aaplFifteenth.UnrealizedPnl);
            Assert.Equal(0m, fifteenth.CashTotalUsd);
            Assert.Equal(22320.00m, fifteenth.TotalValue);
            var correction = await PostTradeAsync(new TradeSubmissionRequest
            {
                ExternalRef = "E2E-TRX-1001",
                AccountId = "ACC-002",
                Instrument = new InstrumentRefRequest
                {
                    Isin = "US0378331005",
                    Symbol = "AAPL"
                },
                Side = "BUY",
                Quantity = 100m,
                Price = 185.40m,
                TradeDate = new DateOnly(2026, 10, 15),
                AsOf = DateTimeOffset.Parse("2026-10-16T09:00:00Z")
            });
            Assert.Equal(HttpStatusCode.OK, correction.Status);
            Assert.Equal(IngestionDecision.CorrectionApplied, correction.Body!.Decision);
            Assert.True(correction.Body.IsApplied);
            var fifteenthAfterCorrection = await GetSnapshotAsync("ACC-002", "2026-10-15");
            Assert.Equal(120m, Assert.Single(fifteenthAfterCorrection.Positions).Quantity);
            Assert.Equal(22320.00m, fifteenthAfterCorrection.TotalMarketValue);
            var sixteenth = await GetSnapshotAsync("ACC-002", "2026-10-16");
            var aaplSixteenth = Assert.Single(sixteenth.Positions);
            Assert.Equal(100m, aaplSixteenth.Quantity);
            Assert.Equal(18600.00m, aaplSixteenth.MarketValue);
            Assert.Equal(60.00m, aaplSixteenth.UnrealizedPnl);
            var fee = await PostCashAsync(new CashSubmissionRequest
            {
                ExternalRef = "E2E-FEE-1001",
                AccountId = "ACC-002",
                Type = "FEE",
                Amount = -15.00m,
                Currency = "USD",
                TradeDate = new DateOnly(2026, 10, 15),
                AsOf = DateTimeOffset.Parse("2026-10-15T10:20:00Z"),
                Description = "Brokerage fee"
            });
            Assert.Equal(HttpStatusCode.Created, fee.Status);
            Assert.True(fee.Body!.IsApplied);
            var fifteenthWithFee = await GetSnapshotAsync("ACC-002", "2026-10-15");
            Assert.Equal(120m, Assert.Single(fifteenthWithFee.Positions).Quantity);
            Assert.Equal(-15.00m, fifteenthWithFee.CashTotalUsd);
            Assert.Equal(22305.00m, fifteenthWithFee.TotalValue);
            var sixteenthWithFee = await GetSnapshotAsync("ACC-002", "2026-10-16");
            Assert.Equal(100m, Assert.Single(sixteenthWithFee.Positions).Quantity);
            Assert.Equal(-15.00m, sixteenthWithFee.CashTotalUsd);
            Assert.Equal(18585.00m, sixteenthWithFee.TotalValue);
        }

        private async Task<(HttpStatusCode Status, TradeSubmissionResponse? Body)> PostTradeAsync(TradeSubmissionRequest request)
        {
            var response = await _client.PostAsJsonAsync("/api/trades", request);
            var body = await response.Content.ReadFromJsonAsync<TradeSubmissionResponse>(JsonOptions);
            return (response.StatusCode, body);
        }

        private async Task<(HttpStatusCode Status, CashSubmissionResponse? Body)> PostCashAsync(CashSubmissionRequest request)
        {
            var response = await _client.PostAsJsonAsync("/api/cash", request);
            var body = await response.Content.ReadFromJsonAsync<CashSubmissionResponse>(JsonOptions);
            return (response.StatusCode, body);
        }

        private async Task<PortfolioSnapshot> GetSnapshotAsync(string accountCode, string date)
        {
            var response = await _client.GetAsync($"/api/accounts/{accountCode}/snapshots?date={date}");
            response.EnsureSuccessStatusCode();
            var snapshot = await response.Content.ReadFromJsonAsync<PortfolioSnapshot>(JsonOptions);
            Assert.NotNull(snapshot);
            return snapshot;
        }
    }
}
