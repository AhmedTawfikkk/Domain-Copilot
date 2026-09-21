using System.Diagnostics;
using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Entites;

namespace DomainCopilot.Infrastructure.Persistence.Repositories;

public sealed class ReviewRunTracker : IReviewRunTracker
{
    private readonly IReviewRunRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ReviewRunTracker(IReviewRunRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<ReviewRun> StartAsync(Guid documentId, string initiatedBy, CancellationToken cancellationToken = default)
    {
        var run = ReviewRun.Start(documentId, initiatedBy, Activity.Current?.GetTagItem("correlation.id") as string, Now());
        await _repository.AddAsync(run, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task<ReviewAgentStep> StartStepAsync(ReviewRun run, string agentName, int sequence, CancellationToken cancellationToken = default)
    {
        var step = run.StartStep(agentName, sequence, Now());
        await _repository.AddStepAsync(step, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return step;
    }

    public async Task CompleteStepAsync(ReviewAgentStep step, int inputItemCount, int outputItemCount, CancellationToken cancellationToken = default)
    {
        step.Complete(inputItemCount, outputItemCount, Now());
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task FailStepAsync(ReviewAgentStep step, string reason, CancellationToken cancellationToken = default)
    {
        step.Fail(reason, Now());
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelStepAsync(ReviewAgentStep step, string reason, CancellationToken cancellationToken = default)
    {
        step.Cancel(reason, Now());
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(ReviewRun run, Guid reviewMemoId, CancellationToken cancellationToken = default)
    {
        run.Complete(reviewMemoId, Now());
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task TerminateAsync(ReviewRun run, string reason, CancellationToken cancellationToken = default)
    {
        run.Terminate(reason, Now());
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(ReviewRun run, string reason, CancellationToken cancellationToken = default)
    {
        run.Cancel(reason, Now());
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
