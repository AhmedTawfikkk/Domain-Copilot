using DomainCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Api.Health;

public sealed class DatabaseMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<DomainCopilotDbContext>();

        _logger.LogInformation("Applying pending database migrations.");

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Database migrations are up to date.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
