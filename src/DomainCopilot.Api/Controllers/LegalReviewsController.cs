using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LegalReviewsController : ControllerBase
{
    private readonly ILegalReviewOrchestrator _orchestrator;

    public LegalReviewsController(
        ILegalReviewOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost]
    [EnableRateLimiting("expensive-operations")]
    public async Task<ActionResult<LegalReviewResult>> Review(
        [FromBody] LegalReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _orchestrator.ReviewAsync(
                request,
                cancellationToken);

            return result.Status == LegalReviewStatus.Terminated
                ? UnprocessableEntity(result)
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = exception.Message
                });
        }
    }
}
