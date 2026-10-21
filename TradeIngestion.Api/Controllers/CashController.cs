using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using TradeIngestion.Api.Cash;
using TradeIngestion.Api.Features;
using TradeIngestion.Core.Ingestion;

namespace TradeIngestion.Api.Controllers
{
    [ApiController]
    [Route("api/cash")]
    /// <summary>HTTP entry for incoming fees / cash adjustments. Same duplicate/correction flag as trades.</summary>
    public class CashController : ControllerBase
    {
        private readonly ICashIngestionService _ingestion;
        private readonly IFeatureManager _featureManager;
        public CashController(ICashIngestionService ingestion,IFeatureManager featureManager)
        {
            _ingestion = ingestion;
            _featureManager = featureManager;
        }

        [HttpPost]
        [ProducesResponseType(typeof(CashSubmissionResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(CashSubmissionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Submit([FromBody] CashSubmissionRequest request,CancellationToken cancellationToken)
        {
            var featureEnabled = await _featureManager.IsEnabledAsync(FeatureFlags.DuplicateAndCorrectionHandling);
            var command = new IngestCashCommand
            {
                ExternalRef = request.ExternalRef,
                AccountCode = request.AccountId,
                Type = request.Type,
                Amount = request.Amount,
                Currency = request.Currency,
                TradeDate = request.TradeDate,
                AsOf = request.AsOf,
                Description = request.Description
            };
            var result = await _ingestion.IngestAsync(command, featureEnabled, cancellationToken);
            if (!result.Succeeded)
            {
                return Problem(
                    title: result.ErrorTitle,
                    detail: result.ErrorDetail,
                    statusCode: result.StatusCode);
            }
            var body = new CashSubmissionResponse
            {
                Id = result.EventId!.Value,
                ExternalRef = result.ExternalRef!,
                Decision = result.Decision!.Value,
                IsApplied = result.IsApplied!.Value
            };
            if (result.StatusCode == StatusCodes.Status201Created)
            {
                return Created($"/api/cash/{body.Id}", body);
            }
            return Ok(body);
        }
    }
}
