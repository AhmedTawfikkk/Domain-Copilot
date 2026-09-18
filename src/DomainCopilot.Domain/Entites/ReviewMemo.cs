using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Entites;

public sealed class ReviewMemo
{
    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    public string OriginalContent { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public MemoApprovalStatus ApprovalStatus { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    public string? DecidedBy { get; private set; }

    public string? DecisionComment { get; private set; }

    public ICollection<ReviewMemoCitation> Citations { get; private set; } =
        new List<ReviewMemoCitation>();

    public ICollection<ReviewMemoRiskFinding> RiskFindings { get; private set; } =
        new List<ReviewMemoRiskFinding>();

    private ReviewMemo()
    {
    }

    public static ReviewMemo CreateDraft(
        Guid documentId,
        string content,
        IReadOnlyList<Guid> citationChunkIds,
        IReadOnlyList<ReviewMemoRiskFindingDraft> riskFindingDrafts,
        DateTime createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            documentId,
            Guid.Empty);

        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentNullException.ThrowIfNull(citationChunkIds);
        ArgumentNullException.ThrowIfNull(riskFindingDrafts);

        var distinctCitationChunkIds = citationChunkIds
            .Where(chunkId => chunkId != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctCitationChunkIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one source citation is required.",
                nameof(citationChunkIds));
        }

        var memo = new ReviewMemo
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            OriginalContent = content.Trim(),
            Content = content.Trim(),
            ApprovalStatus = MemoApprovalStatus.Draft,
            CreatedAtUtc = createdAtUtc
        };

        foreach (var citationChunkId in distinctCitationChunkIds)
        {
            memo.Citations.Add(
                ReviewMemoCitation.Create(
                    memo.Id,
                    citationChunkId,
                    memo.Citations.Count));
        }

        foreach (var riskFindingDraft in riskFindingDrafts)
        {
            memo.RiskFindings.Add(
                ReviewMemoRiskFinding.Create(
                    memo.Id,
                    riskFindingDraft,
                    memo.RiskFindings.Count));
        }

        return memo;
    }

    public void Approve(
        string counselName,
        string? comment,
        DateTime decidedAtUtc)
    {
        EnsureDraft();

        DecidedBy = ValidateCounselName(counselName);
        DecisionComment = NormalizeOptionalText(comment);
        DecidedAtUtc = decidedAtUtc;
        ApprovalStatus = MemoApprovalStatus.Approved;
    }

    public void Reject(
        string counselName,
        string comment,
        DateTime decidedAtUtc)
    {
        EnsureDraft();

        DecidedBy = ValidateCounselName(counselName);

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException(
                "A rejection comment is required.",
                nameof(comment));
        }

        DecisionComment = comment.Trim();
        DecidedAtUtc = decidedAtUtc;
        ApprovalStatus = MemoApprovalStatus.Rejected;
    }

    public void EditAndApprove(
        string counselName,
        string revisedContent,
        string? comment,
        DateTime decidedAtUtc)
    {
        EnsureDraft();

        DecidedBy = ValidateCounselName(counselName);

        if (string.IsNullOrWhiteSpace(revisedContent))
        {
            throw new ArgumentException(
                "Revised memo content is required.",
                nameof(revisedContent));
        }

        Content = revisedContent.Trim();
        DecisionComment = NormalizeOptionalText(comment);
        DecidedAtUtc = decidedAtUtc;
        ApprovalStatus = MemoApprovalStatus.Approved;
    }

    private void EnsureDraft()
    {
        if (ApprovalStatus != MemoApprovalStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft memo can be decided by counsel.");
        }
    }

    private static string ValidateCounselName(string counselName)
    {
        if (string.IsNullOrWhiteSpace(counselName))
        {
            throw new ArgumentException(
                "Counsel name is required.",
                nameof(counselName));
        }

        return counselName.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
