using DomainCopilot.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DomainCopilot.Api.Health;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly DomainCopilotDbContext _dbContext;
    private readonly ILogger<DatabaseReadinessHealthCheck> _logger;

    public DatabaseReadinessHealthCheck(
        DomainCopilotDbContext dbContext,
        ILogger<DatabaseReadinessHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(
                cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "PostgreSQL readiness check failed.");

            return HealthCheckResult.Unhealthy(
                "PostgreSQL readiness check failed.",
                exception);
        }
    }
}
