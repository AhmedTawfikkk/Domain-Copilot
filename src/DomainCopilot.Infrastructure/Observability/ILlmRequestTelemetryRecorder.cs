namespace DomainCopilot.Infrastructure.Observability;

public interface ILlmRequestTelemetryRecorder
{
    Task RecordAsync(
        LlmRequestTelemetryEntry entry,
        CancellationToken cancellationToken = default);
}

public sealed record LlmRequestTelemetryEntry(
    string Provider,
    string Model,
    string Operation,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    bool Succeeded,
    bool WasCancelled,
    string? FailureReason,
    long DurationMilliseconds);
