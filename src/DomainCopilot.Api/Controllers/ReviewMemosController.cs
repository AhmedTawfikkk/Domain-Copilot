using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("state-changing")]
[Authorize(Policy = ApiAuthorizationPolicies.LawyerOrCounsel)]
public sealed class ReviewMemosController : ControllerBase
{
    private readonly IMemoApprovalService _memoApprovalService;
    private readonly IReviewMemoExportService _memoExportService;

    public ReviewMemosController(
    IMemoApprovalService memoApprovalService,
    IReviewMemoExportService memoExportService)
    {
        _memoApprovalService = memoApprovalService;
        _memoExportService = memoExportService;
    }

    [HttpGet("{memoId:guid}")]
    public async Task<ActionResult<ReviewMemoDetails>> Get(
        Guid memoId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memo = await _memoApprovalService.GetAsync(
                memoId,
                cancellationToken);

            return memo is null
                ? NotFound(new { error = "Review memo was not found." })
                : Ok(memo);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{memoId:guid}/approve")]
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
    [Authorize(Policy = ApiAuthorizationPolicies.Counsel)]
    public async Task<IActionResult> ExportDocx(
    Guid memoId,
    CancellationToken cancellationToken = default)
    {
        try
        {
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
