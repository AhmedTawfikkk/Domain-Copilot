using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.Repositories;

public sealed class ReviewRunRepository : IReviewRunRepository
{
    private readonly DomainCopilotDbContext _dbContext;

    public ReviewRunRepository(DomainCopilotDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(ReviewRun run, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewRuns.AddAsync(run, cancellationToken).AsTask();

    public Task AddStepAsync(ReviewAgentStep step, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewAgentSteps.AddAsync(step, cancellationToken).AsTask();

    public Task<ReviewRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewRuns.Include(run => run.Steps)
            .SingleOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public async Task<ReviewRunTrace?> GetTraceAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.ReviewRuns.AsNoTracking()
            .Join(_dbContext.Documents.AsNoTracking(), reviewRun => reviewRun.DocumentId, document => document.Id,
                (reviewRun, document) => new { reviewRun, document.FileName })
            .SingleOrDefaultAsync(item => item.reviewRun.Id == runId, cancellationToken);

        if (run is null) return null;

        var steps = await _dbContext.ReviewAgentSteps.AsNoTracking()
            .Where(step => step.ReviewRunId == runId).OrderBy(step => step.Sequence)
            .Select(step => new ReviewAgentStepTrace(step.Id, step.AgentName, step.Sequence,
                step.Status.ToString(), step.InputItemCount, step.OutputItemCount,
                step.FailureReason, step.StartedAtUtc, step.CompletedAtUtc))
            .ToListAsync(cancellationToken);

        var item = run.reviewRun;
        return new ReviewRunTrace(new ReviewRunSummary(item.Id, item.DocumentId, run.FileName,
            item.InitiatedBy, item.CorrelationId, item.Status.ToString(), item.StartedAtUtc,
            item.CompletedAtUtc, item.ReviewMemoId, item.TerminationReason), steps);
    }

    public async Task<IReadOnlyList<ReviewRunSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ReviewRuns.AsNoTracking()
            .Join(_dbContext.Documents.AsNoTracking(), run => run.DocumentId, document => document.Id,
                (run, document) => new { run, document.FileName })
            .OrderByDescending(item => item.run.StartedAtUtc)
            .Select(item => new ReviewRunSummary(item.run.Id, item.run.DocumentId, item.FileName,
                item.run.InitiatedBy, item.run.CorrelationId, item.run.Status.ToString(),
                item.run.StartedAtUtc, item.run.CompletedAtUtc, item.run.ReviewMemoId,
                item.run.TerminationReason))
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
