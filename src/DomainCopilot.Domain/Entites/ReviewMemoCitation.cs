namespace DomainCopilot.Domain.Entites;

public sealed class ReviewMemoCitation
{
    public Guid ReviewMemoId { get; private set; }

    public Guid DocumentChunkId { get; private set; }

    public int CitationOrder { get; private set; }

    public ReviewMemo ReviewMemo { get; private set; } = null!;

    public DocumentChunk DocumentChunk { get; private set; } = null!;

    private ReviewMemoCitation()
    {
    }

    internal static ReviewMemoCitation Create(
        Guid reviewMemoId,
        Guid documentChunkId,
        int citationOrder)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            reviewMemoId,
            Guid.Empty);

        ArgumentOutOfRangeException.ThrowIfEqual(
            documentChunkId,
            Guid.Empty);

        if (citationOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(citationOrder));
        }

        return new ReviewMemoCitation
        {
            ReviewMemoId = reviewMemoId,
            DocumentChunkId = documentChunkId,
            CitationOrder = citationOrder
        };
    }
}
