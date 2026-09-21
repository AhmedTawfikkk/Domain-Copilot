using DomainCopilot.Domain.Entites;
using DomainCopilot.Infrastructure.Authentication;
using DomainCopilot.Infrastructure.Persistence.Entites;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence;

public class DomainCopilotDbContext : IdentityDbContext<
    ApplicationUser,
    IdentityRole<Guid>,
    Guid>
{
    public DomainCopilotDbContext(DbContextOptions<DomainCopilotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DocumentChunkEmbedding> DocumentChunkEmbeddings =>
    Set<DocumentChunkEmbedding>();
    public DbSet<ReviewMemo> ReviewMemos => Set<ReviewMemo>();
    public DbSet<ReviewMemoCitation> ReviewMemoCitations =>
    Set<ReviewMemoCitation>();

    public DbSet<ReviewMemoRiskFinding> ReviewMemoRiskFindings =>
        Set<ReviewMemoRiskFinding>();

    public DbSet<LlmRequestTelemetry> LlmRequestTelemetry =>
        Set<LlmRequestTelemetry>();

    public DbSet<ReviewRun> ReviewRuns => Set<ReviewRun>();

    public DbSet<ReviewAgentStep> ReviewAgentSteps => Set<ReviewAgentStep>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("vector");
        builder.ApplyConfigurationsFromAssembly(typeof(DomainCopilotDbContext).Assembly);
    }
}
