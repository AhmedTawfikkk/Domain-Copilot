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
        return _dbContext.ReviewMemos.SingleOrDefaultAsync(
            memo => memo.Id == memoId,
            cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}