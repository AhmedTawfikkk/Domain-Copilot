using System.Diagnostics;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.Entites;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Infrastructure.Observability;

public sealed class LlmRequestTelemetryRecorder : ILlmRequestTelemetryRecorder
{
    private const decimal TokensPerMillion = 1_000_000m;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LlmTelemetryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LlmRequestTelemetryRecorder> _logger;

    public LlmRequestTelemetryRecorder(
        IServiceScopeFactory scopeFactory,
        LlmTelemetryOptions options,
        TimeProvider timeProvider,
        ILogger<LlmRequestTelemetryRecorder> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RecordAsync(
        LlmRequestTelemetryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider
                .GetRequiredService<DomainCopilotDbContext>();

            dbContext.LlmRequestTelemetry.Add(new LlmRequestTelemetry
            {
                Id = Guid.NewGuid(),
                CorrelationId = GetCorrelationId(),
                Provider = entry.Provider,
                Model = entry.Model,
                Operation = entry.Operation,
                InputTokens = entry.InputTokens,
                OutputTokens = entry.OutputTokens,
                TotalTokens = entry.TotalTokens,
                EstimatedCostUsd = CalculateEstimatedCost(entry),
                Succeeded = entry.Succeeded,
                WasCancelled = entry.WasCancelled,
                FailureReason = NormalizeFailureReason(entry.FailureReason),
                DurationMilliseconds = entry.DurationMilliseconds,
                CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to persist LLM request telemetry. Provider: {Provider}; Operation: {Operation}.",
                entry.Provider,
                entry.Operation);
        }
    }

    private decimal? CalculateEstimatedCost(LlmRequestTelemetryEntry entry)
    {
        if (string.Equals(entry.Provider, "Ollama", StringComparison.Ordinal))
        {
            return 0m;
        }

        var (inputRate, outputRate) = entry.Provider switch
        {
            "Gemini" => (
                _options.GeminiInputCostPerMillionTokensUsd,
                _options.GeminiOutputCostPerMillionTokensUsd),
            "Groq" => (
                _options.GroqInputCostPerMillionTokensUsd,
                _options.GroqOutputCostPerMillionTokensUsd),
            _ => ((decimal?)null, (decimal?)null)
        };

        if ((!entry.InputTokens.HasValue && !entry.OutputTokens.HasValue) ||
            !inputRate.HasValue ||
            !outputRate.HasValue)
        {
            return null;
        }

        return ((entry.InputTokens ?? 0) / TokensPerMillion * inputRate.Value) +
               ((entry.OutputTokens ?? 0) / TokensPerMillion * outputRate.Value);
    }

    private static string? GetCorrelationId()
    {
        return Activity.Current?.GetTagItem("correlation.id") as string;
    }

    private static string? NormalizeFailureReason(string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return null;
        }

        return failureReason.Length <= 1_000
            ? failureReason
            : failureReason[..1_000];
    }
}
