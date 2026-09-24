using DomainCopilot.Domain.Entites;
using DomainCopilot.Domain.Enums;
using System.Linq;

namespace DomainCopilot.Application.Documents.Review;

public sealed class MemoApprovalService : IMemoApprovalService
{
    private readonly IReviewMemoRepository _reviewMemoRepository;

    public MemoApprovalService(
        IReviewMemoRepository reviewMemoRepository)
    {
        _reviewMemoRepository = reviewMemoRepository;
    }

    public async Task<ReviewMemoDetails?> GetAsync(
        Guid memoId,
        CancellationToken cancellationToken = default)
    {
        if (memoId == Guid.Empty)
        {
            throw new ArgumentException(
                "A non-empty memo ID is required.",
                nameof(memoId));
        }

        var memo = await _reviewMemoRepository.GetByIdAsync(
            memoId,
            cancellationToken);

        return memo is null
            ? null
            : Map(memo);
    }

    public async Task<ReviewMemoDetails> ApproveAsync(
        Guid memoId,
        ApproveMemoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var memo = await GetRequiredMemoAsync(
            memoId,
            cancellationToken);

        memo.Approve(
            request.CounselName,
            request.Comment,
            DateTime.UtcNow);

        await _reviewMemoRepository.SaveChangesAsync(
            cancellationToken);

        return Map(memo);
    }

    public async Task<ReviewMemoDetails> RejectAsync(
        Guid memoId,
        RejectMemoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var memo = await GetRequiredMemoAsync(
            memoId,
            cancellationToken);

        memo.Reject(
            request.CounselName,
            request.Comment,
            DateTime.UtcNow);

        await _reviewMemoRepository.SaveChangesAsync(
            cancellationToken);

        return Map(memo);
    }

    public async Task<ReviewMemoDetails> EditAndApproveAsync(
        Guid memoId,
        EditAndApproveMemoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var memo = await GetRequiredMemoAsync(
            memoId,
            cancellationToken);

        memo.EditAndApprove(
            request.CounselName,
            request.RevisedContent,
            request.Comment,
            DateTime.UtcNow);

        await _reviewMemoRepository.SaveChangesAsync(
            cancellationToken);

        return Map(memo);
    }

    public async Task<ReviewMemoDetails> GetApprovedForFinalizationAsync(
        Guid memoId,
        CancellationToken cancellationToken = default)
    {
        var memo = await GetRequiredMemoAsync(
            memoId,
            cancellationToken);

        if (memo.ApprovalStatus != MemoApprovalStatus.Approved)
        {
            throw new InvalidOperationException(
                "Counsel approval is required before a memo can be finalized, exported, or sent.");
        }

        return Map(memo);
    }

    private async Task<ReviewMemo> GetRequiredMemoAsync(
        Guid memoId,
        CancellationToken cancellationToken)
    {
        if (memoId == Guid.Empty)
        {
            throw new ArgumentException(
                "A non-empty memo ID is required.",
                nameof(memoId));
        }

        var memo = await _reviewMemoRepository.GetByIdAsync(
            memoId,
            cancellationToken);

        return memo ?? throw new KeyNotFoundException(
            $"Review memo '{memoId}' was not found.");
    }

    private static ReviewMemoDetails Map(ReviewMemo memo)
    {
        return new ReviewMemoDetails(
            memo.Id,
            memo.DocumentId,
            memo.OriginalContent,
            memo.Content,
            memo.ApprovalStatus,
            memo.CreatedAtUtc,
            memo.DecidedAtUtc,
            memo.DecidedBy,
            memo.DecisionComment,
            memo.Citations
                .OrderBy(citation => citation.CitationOrder)
                .Select(citation => new MemoCitationExportSource(
                    citation.DocumentChunkId,
                    citation.CitationOrder,
                    citation.DocumentChunk?.Document?.FileName ?? string.Empty,
                    citation.DocumentChunk?.ClauseOrSection,
                    citation.DocumentChunk?.PageNumber,
                    citation.DocumentChunk?.ExtractionConfidence ?? 1.0,
                    citation.DocumentChunk?.LowConfidence ?? false))
                .ToList());
    }
}