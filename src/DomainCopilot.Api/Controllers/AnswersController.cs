using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Answering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.Lawyer)]
public sealed class AnswersController : ControllerBase
{
    private readonly IGroundedAnswerService _groundedAnswerService;

    public AnswersController(
        IGroundedAnswerService groundedAnswerService)
    {
        _groundedAnswerService = groundedAnswerService;
    }

    [HttpPost]
    [EnableRateLimiting("expensive-operations")]
    public async Task<ActionResult<GroundedAnswerResult>> Answer(
        [FromBody] AnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _groundedAnswerService.AnswerAsync(
                request,
                cancellationToken);

            return result.Status == AnswerStatus.Refused
                ? UnprocessableEntity(result)
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = exception.Message });
        }
    }
}
