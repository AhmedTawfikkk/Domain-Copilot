using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.Repositories;

public sealed class ReviewMemoRepository : IReviewMemoRepository
{
    private readonly DomainCopilotDbContext _dbContext;

    public ReviewMemoRepository(
        DomainCopilotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        ReviewMemo memo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memo);

        await _dbContext.ReviewMemos.AddAsync(
            memo,
            cancellationToken);
    }

    public Task<ReviewMemo?> GetByIdAsync(
      Guid memoId,
      CancellationToken cancellationToken = default)
    {
        return _dbContext.ReviewMemos
            .Include(memo => memo.Citations)
                .ThenInclude(citation => citation.DocumentChunk)
                    .ThenInclude(chunk => chunk.Document)
            .Include(memo => memo.RiskFindings)
            .SingleOrDefaultAsync(
                memo => memo.Id == memoId,
                cancellationToken);
    }
    public async Task<ReviewMemoExportSource?> GetExportSourceAsync(
    Guid memoId,
    CancellationToken cancellationToken = default)
    {
        var memo = await _dbContext.ReviewMemos
            .AsNoTracking()
            .Include(item => item.Citations)
                .ThenInclude(citation => citation.DocumentChunk)
                    .ThenInclude(chunk => chunk.Document)
            .Include(item => item.RiskFindings)
            .SingleOrDefaultAsync(
                item => item.Id == memoId,
                cancellationToken);

        if (memo is null)
        {
            return null;
        }

        return new ReviewMemoExportSource(
            memo.Id,
            memo.DocumentId,
            memo.Content,
            memo.CreatedAtUtc,
            memo.DecidedAtUtc,
            memo.DecidedBy,
            memo.Citations
                .OrderBy(citation => citation.CitationOrder)
                .Select(citation => new MemoCitationExportSource(
    citation.DocumentChunkId,
    citation.CitationOrder,
    citation.DocumentChunk.Document.FileName,
    citation.DocumentChunk.ClauseOrSection,
    citation.DocumentChunk.PageNumber,
    citation.DocumentChunk.ExtractionConfidence,
    citation.DocumentChunk.LowConfidence))
                .ToList(),
            memo.RiskFindings
                .OrderBy(finding => finding.DisplayOrder)
                .Select(finding => new MemoRiskFindingExportSource(
                    finding.RuleId,
                    finding.ClauseType,
                    finding.Severity,
                    finding.Title,
                    finding.Rationale,
                    finding.Recommendation,
                    finding.DocumentChunkId,
                    finding.DisplayOrder))
                .ToList());
    }
    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}