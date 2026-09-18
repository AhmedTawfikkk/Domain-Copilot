namespace DomainCopilot.Application.Documents.Review;

public sealed class ReviewMemoExportService
    : IReviewMemoExportService
{
    private readonly IMemoApprovalService _memoApprovalService;
    private readonly IReviewMemoRepository _reviewMemoRepository;
    private readonly IReviewMemoDocxRenderer _docxRenderer;

    public ReviewMemoExportService(
        IMemoApprovalService memoApprovalService,
        IReviewMemoRepository reviewMemoRepository,
        IReviewMemoDocxRenderer docxRenderer)
    {
        _memoApprovalService = memoApprovalService;
        _reviewMemoRepository = reviewMemoRepository;
        _docxRenderer = docxRenderer;
    }

    public async Task<ReviewMemoDocxExport> ExportApprovedDocxAsync(
        Guid memoId,
        CancellationToken cancellationToken = default)
    {
        if (memoId == Guid.Empty)
        {
            throw new ArgumentException(
                "A non-empty memo ID is required.",
                nameof(memoId));
        }

        // This is the fail-closed approval guard.
        await _memoApprovalService.GetApprovedForFinalizationAsync(
            memoId,
            cancellationToken);

        var source = await _reviewMemoRepository.GetExportSourceAsync(
            memoId,
            cancellationToken);

        if (source is null)
        {
            throw new KeyNotFoundException(
                $"Review memo '{memoId}' was not found.");
        }

        if (source.Citations.Count == 0)
        {
            throw new InvalidOperationException(
                "The approved memo cannot be exported because it has no persisted source citations.");
        }
        var citedChunkIds = source.Citations
    .Select(citation => citation.DocumentChunkId)
    .ToHashSet();

        if (source.RiskFindings.Any(finding =>
                finding.DocumentChunkId.HasValue &&
                !citedChunkIds.Contains(finding.DocumentChunkId.Value)))
        {
            throw new InvalidOperationException(
                "The approved memo cannot be exported because a risk finding has no persisted source citation.");
        }
        var content = _docxRenderer.Render(source);

        if (content.Length == 0)
        {
            throw new InvalidOperationException(
                "The DOCX renderer returned an empty document.");
        }

        return new ReviewMemoDocxExport(
            $"review-memo-{memoId:N}.docx",
            content);
    }
}