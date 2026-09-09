using DomainCopilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        string Text);

    public sealed record ChunkDraft(
        string Content,
        string? ClauseOrSection,
        int PageNumber,
        int ChunkIndex,
        Dictionary<string, string> Metadata);

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
            string fileName, Stream content, CancellationToken cancellationToken = default);
    }

    public interface IDocumentIngestionService
    {
        Task<DocumentIngestionResult> IngestAsync(
            IngestDocumentCommand command,
            CancellationToken cancellationToken = default);
    }
}
