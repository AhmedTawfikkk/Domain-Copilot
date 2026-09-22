using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.LawyerOrCounsel)]
public sealed class TelemetryController : ControllerBase
{
    private const int MaxLimit = 200;

    private readonly IReviewRunRepository _repository;

    public TelemetryController(IReviewRunRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Returns persisted per-call LLM telemetry (provider, model, tokens,
    /// estimated cost, duration, success). Optionally filtered by the
    /// correlation id that flows from the request through the orchestrator
    /// to each LLM call.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LlmCallTrace>>> List(
        [FromQuery] string? correlationId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit)
        {
            return BadRequest(new { error = $"Limit must be between 1 and {MaxLimit}." });
        }

        var calls = await _repository.GetLlmCallsAsync(
            correlationId,
            limit,
            cancellationToken);

        return Ok(calls);
    }
}