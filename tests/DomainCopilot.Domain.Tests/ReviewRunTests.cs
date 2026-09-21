using DomainCopilot.Domain.Entites;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Tests;

public sealed class ReviewRunTests
{
    [Fact]
    public void Complete_WhenRunIsRunning_CompletesRunAndStepWithCounts()
    {
        var startedAt = new DateTime(2026, 9, 21, 20, 0, 0, DateTimeKind.Utc);
        var completedAt = startedAt.AddSeconds(3);
        var run = ReviewRun.Start(Guid.NewGuid(), "lawyer@example.com", "correlation-123", startedAt);

        var step = run.StartStep("ClauseExtractor", 1, startedAt);
        step.Complete(4, 3, completedAt);
        run.Complete(Guid.NewGuid(), completedAt);

        Assert.Equal(ReviewRunStatus.Completed, run.Status);
        Assert.Equal(completedAt, run.CompletedAtUtc);
        Assert.NotNull(run.ReviewMemoId);
        Assert.Single(run.Steps);
        Assert.Equal(ReviewAgentStepStatus.Completed, step.Status);
        Assert.Equal(4, step.InputItemCount);
        Assert.Equal(3, step.OutputItemCount);
    }

    [Fact]
    public void Terminate_WhenRunIsRunning_PersistsReasonAndPreventsFurtherSteps()
    {
        var run = ReviewRun.Start(Guid.NewGuid(), "lawyer@example.com", null, DateTime.UtcNow);

        run.Terminate("Clause extractor returned malformed JSON.", DateTime.UtcNow);

        Assert.Equal(ReviewRunStatus.Terminated, run.Status);
        Assert.Equal("Clause extractor returned malformed JSON.", run.TerminationReason);
        Assert.Throws<InvalidOperationException>(() =>
            run.StartStep("RiskAssessor", 2, DateTime.UtcNow));
    }

    [Fact]
    public void Cancel_WhenRunAndStepAreRunning_MarksBothAsCancelled()
    {
        var run = ReviewRun.Start(Guid.NewGuid(), "lawyer@example.com", null, DateTime.UtcNow);
        var step = run.StartStep("ClauseExtractor", 1, DateTime.UtcNow);

        step.Cancel("The legal review was cancelled.", DateTime.UtcNow);
        run.Cancel("The legal review was cancelled.", DateTime.UtcNow);

        Assert.Equal(ReviewAgentStepStatus.Cancelled, step.Status);
        Assert.Equal(ReviewRunStatus.Cancelled, run.Status);
        Assert.Equal("The legal review was cancelled.", run.TerminationReason);
    }
}
