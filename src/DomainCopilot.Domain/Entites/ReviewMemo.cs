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

    private ReviewMemo()
    {
    }

    public static ReviewMemo CreateDraft(
        Guid documentId,
        string content,
        DateTime createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            documentId,
            Guid.Empty);

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new ReviewMemo
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            OriginalContent = content.Trim(),
            Content = content.Trim(),
            ApprovalStatus = MemoApprovalStatus.Draft,
            CreatedAtUtc = createdAtUtc
        };
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