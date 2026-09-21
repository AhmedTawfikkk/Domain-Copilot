using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Entites;

public sealed class ReviewAgentStep
{
    public Guid Id { get; private set; }
    public Guid ReviewRunId { get; private set; }
    public string AgentName { get; private set; } = string.Empty;
    public int Sequence { get; private set; }
    public ReviewAgentStepStatus Status { get; private set; }
    public int? InputItemCount { get; private set; }
    public int? OutputItemCount { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private ReviewAgentStep() { }

    internal static ReviewAgentStep Start(Guid reviewRunId, string agentName, int sequence, DateTime startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        return new ReviewAgentStep { Id = Guid.NewGuid(), ReviewRunId = reviewRunId, AgentName = agentName.Trim(), Sequence = sequence, Status = ReviewAgentStepStatus.Running, StartedAtUtc = startedAtUtc };
    }

    public void Complete(int inputItemCount, int outputItemCount, DateTime completedAtUtc)
    {
        EnsureRunning(); Status = ReviewAgentStepStatus.Completed; InputItemCount = inputItemCount; OutputItemCount = outputItemCount; CompletedAtUtc = completedAtUtc;
    }

    public void Fail(string reason, DateTime completedAtUtc)
    {
        EnsureRunning(); Status = ReviewAgentStepStatus.Failed; FailureReason = reason.Trim(); CompletedAtUtc = completedAtUtc;
    }

    public void Cancel(string reason, DateTime completedAtUtc)
    {
        EnsureRunning(); Status = ReviewAgentStepStatus.Cancelled; FailureReason = reason.Trim(); CompletedAtUtc = completedAtUtc;
    }

    private void EnsureRunning()
    {
        if (Status != ReviewAgentStepStatus.Running) throw new InvalidOperationException("Only a running agent step can be updated.");
    }
}
