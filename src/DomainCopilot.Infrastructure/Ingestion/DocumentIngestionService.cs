using DomainCopilot.Application.Documents.Ingestion;
using DomainCopilot.Domain.Entites;
using DomainCopilot.Domain.Enums;
using DomainCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DomainCopilot.Infrastructure.Ingestion;

public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly DomainCopilotDbContext _dbContext;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly ITextCleaner _textCleaner;
    private readonly IClauseChunker _clauseChunker;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        DomainCopilotDbContext dbContext,
        IDocumentTextExtractor textExtractor,
        ITextCleaner textCleaner,
        IClauseChunker clauseChunker,
        ILogger<DocumentIngestionService> logger)
    {
        _dbContext = dbContext;
        _textExtractor = textExtractor;
        _textCleaner = textCleaner;
        _clauseChunker = clauseChunker;
        _logger = logger;
    }

    public async Task<DocumentIngestionResult> IngestAsync(
        IngestDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Source);

        var sourceType = ResolveSourceType(command.FileName);

        await using var buffer = new MemoryStream();
        await command.Content.CopyToAsync(buffer, cancellationToken);
        var fileBytes = buffer.ToArray();

        if (fileBytes.Length == 0)
        {
            throw new InvalidOperationException("The uploaded document is empty.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(fileBytes));

        var existingDocument = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.FileHash == hash, cancellationToken);

        if (existingDocument is not null)
        {
            var existingChunkCount = await _dbContext.DocumentChunks
                .CountAsync(chunk => chunk.DocumentId == existingDocument.Id, cancellationToken);

            return new DocumentIngestionResult(
                existingDocument.Id,
                existingDocument.FileHash,
                existingDocument.Status,
                IsDuplicate: true,
                ChunkCount: existingChunkCount);
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = command.FileName,
            Source = command.Source,
            SourceType = sourceType,
            Version = "1.0",
            FileHash = hash,
            UploadedAt = DateTime.UtcNow,
            Status = DocumentStatus.Processing
        };

        _dbContext.Documents.Add(document);
       

        try
        {
            await using var stream = new MemoryStream(fileBytes);

            var extractedPages = await _textExtractor.ExtractAsync(
                command.FileName, stream, cancellationToken);

            var cleanedPages = extractedPages
                .Select(page => page with { Text = _textCleaner.Clean(page.Text) })
                .Where(page => !string.IsNullOrWhiteSpace(page.Text))
                .ToList();

            if (cleanedPages.Count == 0)
            {
                throw new InvalidOperationException(
                    "No extractable text was found. The document needs OCR, which will be added later.");
            }

            var chunks = _clauseChunker.Chunk(cleanedPages);

            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("No chunks could be created from this document.");
            }

            foreach (var chunk in chunks)
            {
                document.Chunks.Add(new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Content = chunk.Content,
                    ClauseOrSection = chunk.ClauseOrSection,
                    PageNumber = chunk.PageNumber,
                    ChunkIndex = chunk.ChunkIndex,
                    LowConfidence = false,
                    ExtraMetadata = chunk.Metadata
                });
            }

            document.Status = DocumentStatus.Chunked;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new DocumentIngestionResult(
                document.Id,
                document.FileHash,
                document.Status,
                IsDuplicate: false,
                ChunkCount: chunks.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ingestion failed for document {DocumentId}.", document.Id);

            document.Status = DocumentStatus.Failed;
            document.FailureReason = exception.Message;

            await _dbContext.SaveChangesAsync(CancellationToken.None);

            throw;
        }
    }

    private static DocumentSourceType ResolveSourceType(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "pdf" => DocumentSourceType.Pdf,
            "docx" => DocumentSourceType.Docx,
            "txt" => DocumentSourceType.Txt,
            _ => throw new NotSupportedException($"Unsupported file type: .{extension}")
        };
    }
}