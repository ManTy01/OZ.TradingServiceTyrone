using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using TradeIngestion.Api.Features;
using TradeIngestion.Api.Trades;
using TradeIngestion.Core.Ingestion;

namespace TradeIngestion.Api.Controllers
{
    [ApiController]
    [Route("api/trades")]
    /// <summary>HTTP entry for incoming trades. Maps JSON → command; does not talk to SQL itself.</summary>
    public class TradesController : ControllerBase
    {
        private readonly ITradeIngestionService _ingestion;
        private readonly IFeatureManager _featureManager;
        public TradesController(ITradeIngestionService ingestion,IFeatureManager featureManager)
        {
            _ingestion = ingestion;
            _featureManager = featureManager;
        }

        [HttpPost]
        [ProducesResponseType(typeof(TradeSubmissionResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(TradeSubmissionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Submit([FromBody] TradeSubmissionRequest request,CancellationToken cancellationToken)
        {
            // On = versioned ingest. Off = 409 if externalRef already exists.
            var featureEnabled = await _featureManager.IsEnabledAsync(FeatureFlags.DuplicateAndCorrectionHandling);

            var command = new IngestTradeCommand
            {
                ExternalRef = request.ExternalRef,
                AccountCode = request.AccountId,
                Isin = request.Instrument.Isin,
                Side = request.Side,
                Quantity = request.Quantity,
                Price = request.Price,
                TradeDate = request.TradeDate,
                AsOf = request.AsOf
            };

            var result = await _ingestion.IngestAsync(command, featureEnabled, cancellationToken);
            if (!result.Succeeded)
            {
                return Problem(
                    title: result.ErrorTitle,
                    detail: result.ErrorDetail,
                    statusCode: result.StatusCode);
            }

            var body = new TradeSubmissionResponse
            {
                Id = result.EventId!.Value,
                ExternalRef = result.ExternalRef!,
                Decision = result.Decision!.Value,
                IsApplied = result.IsApplied!.Value
            };

            if (result.StatusCode == StatusCodes.Status201Created)
            {
                return Created($"/api/trades/{body.Id}", body);
            }

            return Ok(body);
        }
    }
}
