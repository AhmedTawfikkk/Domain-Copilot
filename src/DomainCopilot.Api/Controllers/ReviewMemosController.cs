using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Access;
using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.LawyerOrCounsel)]
public sealed class ReviewMemosController : ControllerBase
{
    private readonly IMemoApprovalService _memoApprovalService;
    private readonly IReviewMemoExportService _memoExportService;
    private readonly IDocumentOwnershipRepository _ownershipRepository;

    public ReviewMemosController(
    IMemoApprovalService memoApprovalService,
    IReviewMemoExportService memoExportService,
    IDocumentOwnershipRepository ownershipRepository)
    {
        _memoApprovalService = memoApprovalService;
        _memoExportService = memoExportService;
        _ownershipRepository = ownershipRepository;
    }

    [HttpGet("{memoId:guid}")]
    [EnableRateLimiting("retrieval")]
    public async Task<ActionResult<ReviewMemoDetails>> Get(
        Guid memoId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memo = await _memoApprovalService.GetAsync(
                memoId,
                cancellationToken);

            if (memo is null)
            {
                return NotFound(new { error = "Review memo was not found." });
            }

            if (!await CanAccessMemoAsync(memo, cancellationToken))
            {
                return Forbid();
            }

            return Ok(memo);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{memoId:guid}/approve")]
    [EnableRateLimiting("state-changing")]
    [Authorize(Policy = ApiAuthorizationPolicies.Counsel)]
    public Task<ActionResult<ReviewMemoDetails>> Approve(
        Guid memoId,
        [FromBody] ApproveMemoRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteDecisionAsync(
            () => _memoApprovalService.ApproveAsync(
                memoId,
                request,
                cancellationToken));
    }

    [HttpPost("{memoId:guid}/reject")]
    [EnableRateLimiting("state-changing")]
    [Authorize(Policy = ApiAuthorizationPolicies.Counsel)]
    public Task<ActionResult<ReviewMemoDetails>> Reject(
        Guid memoId,
        [FromBody] RejectMemoRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteDecisionAsync(
            () => _memoApprovalService.RejectAsync(
                memoId,
                request,
                cancellationToken));
    }

    [HttpPost("{memoId:guid}/edit-and-approve")]
    [EnableRateLimiting("state-changing")]
    [Authorize(Policy = ApiAuthorizationPolicies.Counsel)]
    public Task<ActionResult<ReviewMemoDetails>> EditAndApprove(
        Guid memoId,
        [FromBody] EditAndApproveMemoRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteDecisionAsync(
            () => _memoApprovalService.EditAndApproveAsync(
                memoId,
                request,
                cancellationToken));
    }
    [HttpGet("{memoId:guid}/export/docx")]
    [EnableRateLimiting("retrieval")]
    [Authorize(Policy = ApiAuthorizationPolicies.LawyerOrCounsel)]
    public async Task<IActionResult> ExportDocx(
    Guid memoId,
    CancellationToken cancellationToken = default)
    {
        try
        {
            var memo = await _memoApprovalService.GetAsync(
                memoId,
                cancellationToken);

            if (memo is null)
            {
                return NotFound(new
                {
                    error = "Review memo was not found."
                });
            }

            if (!await CanAccessMemoAsync(memo, cancellationToken))
            {
                return Forbid();
            }

            var export = await _memoExportService.ExportApprovedDocxAsync(
                memoId,
                cancellationToken);

            return File(
                export.Content,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                export.FileName);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                error = exception.Message
            });
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
            return UnprocessableEntity(new
            {
                error = exception.Message
            });
        }
    }

    private async Task<bool> CanAccessMemoAsync(
        ReviewMemoDetails memo,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(ApiRoles.Counsel))
        {
            return true;
        }

        var currentUserId = CurrentUserId();

        if (currentUserId is null)
        {
            return false;
        }

        var ownerId = await _ownershipRepository.GetOwnerIdAsync(
            memo.DocumentId,
            cancellationToken);

        return ownerId.HasValue && ownerId.Value == currentUserId.Value;
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private async Task<ActionResult<ReviewMemoDetails>>
        ExecuteDecisionAsync(
            Func<Task<ReviewMemoDetails>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException exception)
        {
            return new NotFoundObjectResult(new
            {
                error = exception.Message
            });
        }
        catch (ArgumentException exception)
        {
            return new BadRequestObjectResult(new
            {
                error = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return new UnprocessableEntityObjectResult(new
            {
                error = exception.Message
            });
        }
    }
}
