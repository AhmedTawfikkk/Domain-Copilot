using DomainCopilot.Domain.Entites;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("vector");
        builder.ApplyConfigurationsFromAssembly(typeof(DomainCopilotDbContext).Assembly);
    }
}
