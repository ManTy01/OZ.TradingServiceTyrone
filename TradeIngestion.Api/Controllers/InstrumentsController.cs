using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeIngestion.Api.Instruments;
using TradeIngestion.Infrastructure;

namespace TradeIngestion.Api.Controllers;

[ApiController]
[Route("api/instruments")]
public class InstrumentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public InstrumentsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InstrumentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InstrumentResponse>>> List(
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var asOf = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var instruments = await _db.Instruments
            .AsNoTracking()
            .OrderBy(i => i.Symbol)
            .ToListAsync(cancellationToken);

        var prices = await _db.MarketPrices
            .AsNoTracking()
            .Where(p => p.AsOfDate <= asOf)
            .ToListAsync(cancellationToken);

        var latestPrices = prices
            .GroupBy(p => p.InstrumentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.AsOfDate).First());

        var fx = await _db.FxRates
            .AsNoTracking()
            .Where(r =>
                r.BaseCurrency == "USD"
                && r.QuoteCurrency == "ZAR"
                && r.AsOfDate <= asOf)
            .OrderByDescending(r => r.AsOfDate)
            .FirstOrDefaultAsync(cancellationToken);

        var list = instruments.Select(instrument =>
        {
            latestPrices.TryGetValue(instrument.Id, out var price);
            decimal? native = price is null
                ? null
                : Math.Round(price.Price, 2, MidpointRounding.AwayFromZero);
            decimal? usd = null;
            if (price is not null)
            {
                if (price.Currency == "USD")
                {
                    usd = native;
                }
                else if (price.Currency == "ZAR" && fx is not null && fx.Rate != 0)
                {
                    // 1 USD = Rate ZAR. ZAR → USD is divide, never multiply.
                    usd = Math.Round(price.Price / fx.Rate, 2, MidpointRounding.AwayFromZero);
                }
            }

            return new InstrumentResponse
            {
                Isin = instrument.Isin,
                Symbol = instrument.Symbol,
                Currency = instrument.Currency,
                MarketPriceNative = native,
                MarketPriceUsd = usd
            };
        }).ToList();

        return Ok(list);
    }
}
