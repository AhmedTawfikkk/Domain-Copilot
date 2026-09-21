namespace DomainCopilot.Infrastructure.Persistence.Entites;

public sealed class LlmRequestTelemetry
{
    public Guid Id { get; set; }

    public string? CorrelationId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public int? TotalTokens { get; set; }

    public decimal? EstimatedCostUsd { get; set; }

    public bool Succeeded { get; set; }

    public bool WasCancelled { get; set; }

    public string? FailureReason { get; set; }

    public long DurationMilliseconds { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
