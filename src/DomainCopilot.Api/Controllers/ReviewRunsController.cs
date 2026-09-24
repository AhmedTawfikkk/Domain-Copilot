using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Access;
using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.LawyerOrCounsel)]
public sealed class ReviewRunsController : ControllerBase
{
    private readonly IReviewRunRepository _repository;
    private readonly IReviewRunCancellationRegistry _cancellationRegistry;
    private readonly IDocumentOwnershipRepository _ownershipRepository;

    public ReviewRunsController(
        IReviewRunRepository repository,
        IReviewRunCancellationRegistry cancellationRegistry,
        IDocumentOwnershipRepository ownershipRepository)
    {
        _repository = repository;
        _cancellationRegistry = cancellationRegistry;
        _ownershipRepository = ownershipRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewRunSummary>>> List(CancellationToken cancellationToken)
    {
        // Counsel sees every run (approval desk). Lawyers see only their own runs.
        var ownerScopeId = User.IsInRole(ApiRoles.Counsel)
            ? (Guid?)null
            : CurrentUserId();

        return Ok(await _repository.ListAsync(ownerScopeId, cancellationToken));
    }

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult<ReviewRunTrace>> Get(Guid runId, CancellationToken cancellationToken)
    {
        var trace = await _repository.GetTraceAsync(runId, cancellationToken);

        if (trace is null)
        {
            return NotFound();
        }

        if (!await IsDocumentOwnerAsync(trace.Run.DocumentId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(trace);
    }

    [HttpPost("{runId:guid}/cancel")]
    [Authorize(Policy = ApiAuthorizationPolicies.Lawyer)]
    public async Task<IActionResult> Cancel(Guid runId, CancellationToken cancellationToken)
    {
        var documentId = await _repository.GetDocumentIdAsync(runId, cancellationToken);

        if (documentId is null)
        {
            return NotFound(new
            {
                error = "No review run exists for the supplied ID."
            });
        }

        if (!await IsDocumentOwnerAsync(documentId.Value, cancellationToken))
        {
            return Forbid();
        }

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

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private async Task<bool> IsDocumentOwnerAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var currentUserId = CurrentUserId();

        if (currentUserId is null)
        {
            return false;
        }

        var ownerId = await _ownershipRepository.GetOwnerIdAsync(
            documentId,
            cancellationToken);

        return ownerId.HasValue && ownerId.Value == currentUserId.Value;
    }
}
