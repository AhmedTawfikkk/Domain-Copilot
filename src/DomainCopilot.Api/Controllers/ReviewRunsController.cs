using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.LawyerOrCounsel)]
public sealed class ReviewRunsController : ControllerBase
{
    private readonly IReviewRunRepository _repository;
    private readonly IReviewRunCancellationRegistry _cancellationRegistry;

    public ReviewRunsController(
        IReviewRunRepository repository,
        IReviewRunCancellationRegistry cancellationRegistry)
    {
        _repository = repository;
        _cancellationRegistry = cancellationRegistry;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewRunSummary>>> List(CancellationToken cancellationToken)
        => Ok(await _repository.ListAsync(cancellationToken));

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult<ReviewRunTrace>> Get(Guid runId, CancellationToken cancellationToken)
    {
        var trace = await _repository.GetTraceAsync(runId, cancellationToken);
        return trace is null ? NotFound() : Ok(trace);
    }

    [HttpPost("{runId:guid}/cancel")]
    [Authorize(Policy = ApiAuthorizationPolicies.Lawyer)]
    public ActionResult Cancel(Guid runId)
    {
        if (!_cancellationRegistry.TryCancel(runId))
        {
            return NotFound(new
            {
                error = "No active review run exists for the supplied ID."
            });
        }

        return Accepted(new
        {
            reviewRunId = runId,
            status = "CancellationRequested"
        });
    }
}
