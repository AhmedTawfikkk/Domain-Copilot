using DomainCopilot.Application.Documents.Access;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.Repositories;

public sealed class DocumentOwnershipRepository : IDocumentOwnershipRepository
{
    private readonly DomainCopilotDbContext _dbContext;

    public DocumentOwnershipRepository(
        DomainCopilotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<Guid?> GetOwnerIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Documents.AsNoTracking()
            .Where(document => document.Id == documentId)
            .Select(document => document.OwnerId)
            .SingleOrDefaultAsync(cancellationToken);
}