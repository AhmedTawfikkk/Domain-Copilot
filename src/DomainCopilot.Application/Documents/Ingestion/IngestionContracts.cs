using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Application.Documents.Ingestion
{
    public sealed record IngestDocumentCommand(
        string FileName,
        string Source,
        Stream Content);

    public sealed record DocumentIngestionResult(
        Guid DocumentId,
        string FileHash,
        DocumentStatus Status,
        bool IsDuplicate,
        int ChunkCount);

    public sealed record ExtractedPage(
        int PageNumber,
        string Text,
        double ExtractionConfidence = 1.0);

    public sealed record ChunkDraft(
        string Content,
        string? ClauseOrSection,
        int PageNumber,
        int ChunkIndex,
        Dictionary<string, string> Metadata,
        double ExtractionConfidence);

    public sealed class DocumentIngestionPolicy
    {
        public double LowConfidenceThreshold { get; init; } = 0.85;

        public void Validate()
        {
            if (LowConfidenceThreshold is < 0.0 or > 1.0)
            {
                throw new InvalidOperationException(
                    "LowConfidenceThreshold must be between 0.0 and 1.0.");
            }
        }
    }

    public interface ITextExtractor
    {
        bool CanExtract(string fileName);

        Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
            Stream content,
            CancellationToken cancellationToken = default);
    }

    public interface ITextCleaner
    {
        string Clean(string text);
    }

    public interface IClauseChunker
    {
        IReadOnlyList<ChunkDraft> Chunk(
            IReadOnlyList<ExtractedPage> pages);
    }

    public interface IDocumentTextExtractor
    {
        Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
            string fileName,
            Stream content,
            CancellationToken cancellationToken = default);
    }
    public interface IPdfOcrService
    {
        Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
            Stream pdfContent,
            CancellationToken cancellationToken = default);
    }

    public interface IDocumentIngestionService
    {
        Task<DocumentIngestionResult> IngestAsync(
            IngestDocumentCommand command,
            CancellationToken cancellationToken = default);
    }
}