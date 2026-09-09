using DomainCopilot.Domain.Entites;
using DomainCopilot.Infrastructure.Persistence.Entites;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence;

public class DomainCopilotDbContext : DbContext
{
    public DomainCopilotDbContext(DbContextOptions<DomainCopilotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DocumentChunkEmbedding> DocumentChunkEmbeddings =>
    Set<DocumentChunkEmbedding>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("vector");
        builder.ApplyConfigurationsFromAssembly(typeof(DomainCopilotDbContext).Assembly);
    }
}
