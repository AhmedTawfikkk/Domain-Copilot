namespace DomainCopilot.Infrastructure.Observability;

public sealed class LlmTelemetryOptions
{
    public decimal? GeminiInputCostPerMillionTokensUsd { get; init; }

    public decimal? GeminiOutputCostPerMillionTokensUsd { get; init; }

    public decimal? GroqInputCostPerMillionTokensUsd { get; init; }

    public decimal? GroqOutputCostPerMillionTokensUsd { get; init; }

    public void Validate()
    {
        if (GeminiInputCostPerMillionTokensUsd < 0 ||
            GeminiOutputCostPerMillionTokensUsd < 0 ||
            GroqInputCostPerMillionTokensUsd < 0 ||
            GroqOutputCostPerMillionTokensUsd < 0)
        {
            throw new InvalidOperationException(
                "LlmTelemetry token prices cannot be negative.");
        }
    }
}
