using DomainCopilot.Application.Documents.Review;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReviewMemosController : ControllerBase
{
    private readonly IMemoApprovalService _memoApprovalService;

    public ReviewMemosController(
        IMemoApprovalService memoApprovalService)
    {
        _memoApprovalService = memoApprovalService;
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
