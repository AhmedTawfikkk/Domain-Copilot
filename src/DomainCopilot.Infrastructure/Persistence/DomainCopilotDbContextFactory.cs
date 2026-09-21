using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DomainCopilot.Infrastructure.Persistence;

/// <summary>
/// Creates the database context for Entity Framework tooling without starting
/// the HTTP application or its runtime-only integrations such as OCR and LLMs.
/// </summary>
public sealed class DomainCopilotDbContextFactory :
    IDesignTimeDbContextFactory<DomainCopilotDbContext>
{
    public DomainCopilotDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "POSTGRES_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "POSTGRES_CONNECTION_STRING is required to run Entity Framework tooling.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<DomainCopilotDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.UseVector());

        return new DomainCopilotDbContext(optionsBuilder.Options);
    }
}
