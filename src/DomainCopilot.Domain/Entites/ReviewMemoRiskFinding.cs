namespace DomainCopilot.Domain.Entites;

public sealed record ReviewMemoRiskFindingDraft(
    string RuleId,
    string ClauseType,
    string Severity,
    string Title,
    string Rationale,
    string Recommendation,
    Guid? DocumentChunkId);

public sealed class ReviewMemoRiskFinding
{
    public Guid Id { get; private set; }

    public Guid ReviewMemoId { get; private set; }

    public string RuleId { get; private set; } = string.Empty;

    public string ClauseType { get; private set; } = string.Empty;

    public string Severity { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Rationale { get; private set; } = string.Empty;

    public string Recommendation { get; private set; } = string.Empty;

    public Guid? DocumentChunkId { get; private set; }

    public int DisplayOrder { get; private set; }

    public ReviewMemo ReviewMemo { get; private set; } = null!;

    private ReviewMemoRiskFinding()
    {
    }

    internal static ReviewMemoRiskFinding Create(
        Guid reviewMemoId,
        ReviewMemoRiskFindingDraft draft,
        int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            reviewMemoId,
            Guid.Empty);

        ArgumentNullException.ThrowIfNull(draft);

        if (string.IsNullOrWhiteSpace(draft.RuleId) ||
            string.IsNullOrWhiteSpace(draft.ClauseType) ||
            string.IsNullOrWhiteSpace(draft.Severity) ||
            string.IsNullOrWhiteSpace(draft.Title) ||
            string.IsNullOrWhiteSpace(draft.Rationale) ||
            string.IsNullOrWhiteSpace(draft.Recommendation))
        {
            throw new ArgumentException(
                "A risk finding must contain all required fields.",
                nameof(draft));
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder));
        }

        return new ReviewMemoRiskFinding
        {
            Id = Guid.NewGuid(),
            ReviewMemoId = reviewMemoId,
            RuleId = draft.RuleId.Trim(),
            ClauseType = draft.ClauseType.Trim(),
            Severity = draft.Severity.Trim(),
            Title = draft.Title.Trim(),
            Rationale = draft.Rationale.Trim(),
            Recommendation = draft.Recommendation.Trim(),
            DocumentChunkId = draft.DocumentChunkId,
            DisplayOrder = displayOrder
        };
    }
}
