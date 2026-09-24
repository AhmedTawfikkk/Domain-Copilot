using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Access;
using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text.Json;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.Lawyer)]
public sealed class LegalReviewsController : ControllerBase
{
    private readonly ILegalReviewOrchestrator _orchestrator;
    private readonly IStreamingLegalReviewService _streamingLegalReviewService;
    private readonly IDocumentOwnershipRepository _ownershipRepository;

    public LegalReviewsController(
        ILegalReviewOrchestrator orchestrator,
        IStreamingLegalReviewService streamingLegalReviewService,
        IDocumentOwnershipRepository ownershipRepository)
    {
        _orchestrator = orchestrator;
        _streamingLegalReviewService = streamingLegalReviewService;
        _ownershipRepository = ownershipRepository;
    }

    [HttpPost]
    [EnableRateLimiting("expensive-operations")]
    public async Task<ActionResult<LegalReviewResult>> Review(
        [FromBody] LegalReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsDocumentOwnerAsync(request.DocumentId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var result = await _orchestrator.ReviewAsync(
                request with
                {
                    InitiatedBy = User.Identity?.Name ?? "unknown"
                },
                cancellationToken);

            return result.Status switch
            {
                LegalReviewStatus.Completed => Ok(result),
                LegalReviewStatus.Cancelled => Conflict(result),
                _ => UnprocessableEntity(result)
            };
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

    [HttpPost("stream")]
    [EnableRateLimiting("expensive-operations")]
    public async Task Stream(
        [FromBody] LegalReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        if (!await IsDocumentOwnerAsync(request.DocumentId, cancellationToken))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await WriteErrorAsync(
                "forbidden",
                "You do not have access to the document for this review request.",
                cancellationToken);

            return;
        }

        try
        {
            var streamRequest = request with
            {
                InitiatedBy = User.Identity?.Name ?? "unknown"
            };

            await foreach (var progressEvent in _streamingLegalReviewService
                               .StreamAsync(streamRequest, cancellationToken)
                               .WithCancellation(cancellationToken))
            {
                await Response.WriteAsync(
                    $"event: {ToEventName(progressEvent.Type)}\n" +
                    $"data: {JsonSerializer.Serialize(progressEvent)}\n\n",
                    cancellationToken);

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (ArgumentException exception)
        {
            await WriteErrorAsync("bad-request", exception.Message, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await WriteErrorAsync("unavailable", exception.Message, cancellationToken);
        }
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

    private static string ToEventName(LegalReviewProgressEventType type) => type switch
    {
        LegalReviewProgressEventType.Started => "started",
        LegalReviewProgressEventType.AgentStarted => "progress",
        LegalReviewProgressEventType.AgentCompleted => "progress",
        LegalReviewProgressEventType.Completed => "completed",
        LegalReviewProgressEventType.Terminated => "terminated",
        LegalReviewProgressEventType.Cancelled => "cancelled",
        _ => "error"
    };

    private async Task WriteErrorAsync(
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        await Response.WriteAsync(
            $"event: error\n" +
            $"data: {JsonSerializer.Serialize(new { code, message })}\n\n",
            cancellationToken);

        await Response.Body.FlushAsync(cancellationToken);
    }
}
