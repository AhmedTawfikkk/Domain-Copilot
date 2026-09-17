using DomainCopilot.Application.Documents.Review;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.Repositories;

public sealed class DocumentReviewRepository : IDocumentReviewRepository
{
    private readonly DomainCopilotDbContext _dbContext;

    public DocumentReviewRepository(
        DomainCopilotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReviewDocument?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .Where(item => item.Id == documentId)
            .Select(item => new
            {
                item.Id,
                item.FileName,
                item.Source,
                item.Version,
                item.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return null;
        }

        var chunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new ReviewSourceChunk(
                chunk.Id,
                chunk.Content,
                chunk.ClauseOrSection,
                chunk.PageNumber,
                chunk.LowConfidence,
                chunk.ChunkIndex))
            .ToListAsync(cancellationToken);

        return new ReviewDocument(
            document.Id,
            document.FileName,
            document.Source,
            document.Version,
            document.Status,
            chunks);
    }
}
