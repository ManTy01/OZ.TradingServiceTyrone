using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeIngestion.Api.Accounts;
using TradeIngestion.Core.Snapshot;
using TradeIngestion.Infrastructure;

namespace TradeIngestion.Api.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    /// <summary>Read side: list accounts, snapshot, recent events (including duplicates for the UI).</summary>
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ISnapshotService _snapshots;
        public AccountsController(AppDbContext db, ISnapshotService snapshots)
        {
            _db = db;
            _snapshots = snapshots;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<AccountSummaryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<AccountSummaryResponse>>> List(CancellationToken cancellationToken)
        {
            var accounts = await _db.Accounts
                .AsNoTracking()
                .OrderBy(a => a.AccountCode)
                .Select(a => new AccountSummaryResponse
                {
                    AccountCode = a.AccountCode,
                    Name = a.Name
                })
                .ToListAsync(cancellationToken);
            return Ok(accounts);
        }

        [HttpGet("{accountCode}/snapshots")]
        [ProducesResponseType(typeof(PortfolioSnapshot), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSnapshot(string accountCode,[FromQuery] DateOnly date,CancellationToken cancellationToken)
        {
            if (date == default)
            {
                return Problem(
                    title: "Validation failed",
                    detail: "Query parameter 'date' is required (yyyy-MM-dd).",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            var result = await _snapshots.GetAsync(accountCode, date, cancellationToken);
            if (!result.Succeeded)
            {
                return Problem(
                    title: result.ErrorTitle,
                    detail: result.ErrorDetail,
                    statusCode: result.StatusCode);
            }
            return Ok(result.Snapshot);
        }

        [HttpGet("{accountCode}/events")]
        [ProducesResponseType(typeof(IReadOnlyList<AccountEventResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvents(string accountCode,[FromQuery] int take = 50,CancellationToken cancellationToken = default)
        {
            var account = await _db.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountCode == accountCode, cancellationToken);

            if (account is null)
            {
                return Problem(
                    title: "Unknown account",
                    detail: $"No account exists with code '{accountCode}'.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            take = Math.Clamp(take, 1, 200);

            var trades = await _db.TradeEvents
                .AsNoTracking()
                .Where(e => e.AccountId == account.Id)
                .OrderByDescending(e => e.ReceivedAt)
                .Take(take)
                .Select(e => new AccountEventResponse
                {
                    Id = e.Id,
                    Kind = "Trade",
                    ExternalRef = e.ExternalRef,
                    TradeDate = e.TradeDate,
                    AsOf = e.AsOf,
                    ReceivedAt = e.ReceivedAt,
                    Decision = e.Decision,
                    IsApplied = e.IsApplied,
                    InstrumentIsin = e.Instrument.Isin,
                    InstrumentSymbol = e.Instrument.Symbol,
                    Side = e.Side.ToString(),
                    Quantity = e.Quantity,
                    Price = e.Price,
                    Currency = e.Currency
                })
                .ToListAsync(cancellationToken);

            var cash = await _db.CashEvents
                .AsNoTracking()
                .Where(e => e.AccountId == account.Id)
                .OrderByDescending(e => e.ReceivedAt)
                .Take(take)
                .Select(e => new AccountEventResponse
                {
                    Id = e.Id,
                    Kind = "Cash",
                    ExternalRef = e.ExternalRef,
                    TradeDate = e.TradeDate,
                    AsOf = e.AsOf,
                    ReceivedAt = e.ReceivedAt,
                    Decision = e.Decision,
                    IsApplied = e.IsApplied,
                    CashType = e.Type.ToString(),
                    Amount = e.Amount,
                    Currency = e.Currency,
                    Description = e.Description
                })
                .ToListAsync(cancellationToken);

            var events = trades
                .Concat(cash)
                .OrderByDescending(e => e.ReceivedAt)
                .Take(take)
                .ToList();

            return Ok(events);
        }
    }
}
