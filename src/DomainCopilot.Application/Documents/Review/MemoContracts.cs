using DomainCopilot.Domain.Entites;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Application.Documents.Review;

public sealed record MemoDraftRequest(
    Guid DocumentId,
    string FileName,
    string Source,
    string Version,
    IReadOnlyList<ExtractedClause> ExtractedClauses,
    IReadOnlyList<RiskFinding> RiskFindings);

public sealed record MemoDraftResult(
    bool Succeeded,
    string? Content,
    IReadOnlyList<Guid> CitationChunkIds,
    string? FailureReason);

public sealed record MemoDraftPrompts(
    string SystemPrompt,
    string UserPrompt);
public sealed record ReviewMemoDraft(
    Guid MemoId,
    string Content,
    IReadOnlyList<Guid> CitationChunkIds,
    MemoApprovalStatus ApprovalStatus,
    DateTime CreatedAtUtc);

public sealed record ReviewMemoDetails(
    Guid MemoId,
    Guid DocumentId,
    string OriginalContent,
    string Content,
    MemoApprovalStatus ApprovalStatus,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecidedBy,
    string? DecisionComment,
    IReadOnlyList<MemoCitationExportSource> Citations);

public sealed record ApproveMemoRequest(
    string CounselName,
    string? Comment);

public sealed record RejectMemoRequest(
    string CounselName,
    string Comment);

public sealed record EditAndApproveMemoRequest(
    string CounselName,
    string RevisedContent,
    string? Comment);
public interface IMemoDraftPromptTemplate
{
    string Version { get; }

    MemoDraftPrompts Render(MemoDraftRequest request);
}

public interface IMemoDrafterAgent
{
    Task<MemoDraftResult> DraftAsync(
        MemoDraftRequest request,
        CancellationToken cancellationToken = default);
}
public interface IReviewMemoRepository
{
    Task AddAsync(
        ReviewMemo memo,
        CancellationToken cancellationToken = default);

    Task<ReviewMemo?> GetByIdAsync(
        Guid memoId,
        CancellationToken cancellationToken = default);
    Task<ReviewMemoExportSource?> GetExportSourceAsync(
    Guid memoId,
    CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);


}
public interface IMemoApprovalService
{
    Task<ReviewMemoDetails?> GetAsync(
        Guid memoId,
        CancellationToken cancellationToken = default);

    Task<ReviewMemoDetails> ApproveAsync(
        Guid memoId,
        ApproveMemoRequest request,
        CancellationToken cancellationToken = default);

    Task<ReviewMemoDetails> RejectAsync(
        Guid memoId,
        RejectMemoRequest request,
        CancellationToken cancellationToken = default);

    Task<ReviewMemoDetails> EditAndApproveAsync(
        Guid memoId,
        EditAndApproveMemoRequest request,
        CancellationToken cancellationToken = default);

    Task<ReviewMemoDetails> GetApprovedForFinalizationAsync(
        Guid memoId,
        CancellationToken cancellationToken = default);
}