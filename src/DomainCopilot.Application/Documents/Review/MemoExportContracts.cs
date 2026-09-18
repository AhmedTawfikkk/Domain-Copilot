namespace DomainCopilot.Application.Documents.Review;

public sealed record MemoCitationExportSource(
    Guid DocumentChunkId,
    int CitationOrder,
    string FileName,
    string? ClauseOrSection,
    int? PageNumber,
    double ExtractionConfidence,
    bool LowConfidence);

public sealed record MemoRiskFindingExportSource(
    string RuleId,
    string ClauseType,
    string Severity,
    string Title,
    string Rationale,
    string Recommendation,
    Guid? DocumentChunkId,
    int DisplayOrder);

public sealed record ReviewMemoExportSource(
    Guid MemoId,
    Guid DocumentId,
    string Content,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecidedBy,
    IReadOnlyList<MemoCitationExportSource> Citations,
    IReadOnlyList<MemoRiskFindingExportSource> RiskFindings);

public sealed record ReviewMemoDocxExport(
    string FileName,
    byte[] Content);

public interface IReviewMemoDocxRenderer
{
    byte[] Render(ReviewMemoExportSource source);
}

public interface IReviewMemoExportService
{
    Task<ReviewMemoDocxExport> ExportApprovedDocxAsync(
        Guid memoId,
        CancellationToken cancellationToken = default);
}