using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Entites;

public sealed class ReviewRun
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string InitiatedBy { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public ReviewRunStatus Status { get; private set; }
    public string? TerminationReason { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid? ReviewMemoId { get; private set; }
    public ICollection<ReviewAgentStep> Steps { get; private set; } = new List<ReviewAgentStep>();

    private ReviewRun() { }

    public static ReviewRun Start(Guid documentId, string initiatedBy, string? correlationId, DateTime startedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(documentId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatedBy);

        return new ReviewRun
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            InitiatedBy = initiatedBy.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            Status = ReviewRunStatus.Running,
            StartedAtUtc = startedAtUtc
        };
    }

    public ReviewAgentStep StartStep(string agentName, int sequence, DateTime startedAtUtc)
    {
        EnsureRunning();
        var step = ReviewAgentStep.Start(Id, agentName, sequence, startedAtUtc);
        Steps.Add(step);
        return step;
    }

    public void Complete(Guid reviewMemoId, DateTime completedAtUtc)
    {
        EnsureRunning();
        ReviewMemoId = reviewMemoId;
        Status = ReviewRunStatus.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    public void Terminate(string reason, DateTime completedAtUtc)
    {
        EnsureRunning();
        TerminationReason = reason.Trim();
        Status = ReviewRunStatus.Terminated;
        CompletedAtUtc = completedAtUtc;
    }

    public void Cancel(string reason, DateTime completedAtUtc)
    {
        EnsureRunning();
        TerminationReason = reason.Trim();
        Status = ReviewRunStatus.Cancelled;
        CompletedAtUtc = completedAtUtc;
    }

    private void EnsureRunning()
    {
        if (Status != ReviewRunStatus.Running)
            throw new InvalidOperationException("Only a running review can be updated.");
    }
}
