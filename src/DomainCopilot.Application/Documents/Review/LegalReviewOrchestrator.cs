using System.Diagnostics;
using DomainCopilot.Domain.Entites;
using DomainCopilot.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Documents.Review;

public sealed class LegalReviewOrchestrator : ILegalReviewOrchestrator
{
    private readonly IDocumentReviewRepository _documentReviewRepository;
    private readonly IClauseExtractorAgent _clauseExtractorAgent;
    private readonly IRiskAssessorAgent _riskAssessorAgent;
    private readonly IMemoDrafterAgent _memoDrafterAgent;
    private readonly IReviewMemoRepository _reviewMemoRepository;
    private readonly IReviewRunTracker _reviewRunTracker;
    private readonly IReviewRunCancellationRegistry _cancellationRegistry;
    private readonly ReviewExecutionPolicy _policy;
    private readonly ILogger<LegalReviewOrchestrator> _logger;

    public LegalReviewOrchestrator(
        IDocumentReviewRepository documentReviewRepository,
        IClauseExtractorAgent clauseExtractorAgent,
        IRiskAssessorAgent riskAssessorAgent,
        IMemoDrafterAgent memoDrafterAgent,
        IReviewMemoRepository reviewMemoRepository,
        IReviewRunTracker reviewRunTracker,
        IReviewRunCancellationRegistry cancellationRegistry,
        ReviewExecutionPolicy policy,
        ILogger<LegalReviewOrchestrator> logger)
    {
        _documentReviewRepository = documentReviewRepository;
        _clauseExtractorAgent = clauseExtractorAgent;
        _riskAssessorAgent = riskAssessorAgent;
        _memoDrafterAgent = memoDrafterAgent;
        _reviewMemoRepository = reviewMemoRepository;
        _reviewRunTracker = reviewRunTracker;
        _cancellationRegistry = cancellationRegistry;
        _policy = policy;
        _logger = logger;
    }

    public async Task<LegalReviewResult> ReviewAsync(
        LegalReviewRequest request,
        CancellationToken cancellationToken = default,
        IReviewProgressReporter? progressReporter = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DocumentId == Guid.Empty)
        {
            return Terminate(
                request.DocumentId,
                null,
                ReviewTerminationReason.InvalidRequest,
                "A non-empty document ID is required.");
        }

        var document = await _documentReviewRepository.GetDocumentAsync(
            request.DocumentId,
            cancellationToken);

        if (document is null)
        {
            return Terminate(
                request.DocumentId,
                null,
                ReviewTerminationReason.DocumentNotFound,
                "The requested document was not found.");
        }

        if (document.Status == DocumentStatus.Failed)
        {
            return Terminate(
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.DocumentFailed,
                "The requested document is marked as failed.");
        }

        if (document.Chunks.Count == 0)
        {
            return Terminate(
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.DocumentHasNoChunks,
                "The requested document has no extracted chunks.");
        }

        if (document.Chunks.Count > _policy.MaxChunksPerReview)
        {
            return Terminate(
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MaximumChunkCountExceeded,
                $"The document contains more than {_policy.MaxChunksPerReview} chunks.");
        }

        var batches = document.Chunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .Chunk(_policy.MaxChunksPerExtractionBatch)
            .ToList();

        if (batches.Count > _policy.MaxExtractionBatches)
        {
            return Terminate(
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MaximumExtractionBatchCountExceeded,
                $"The review would require more than {_policy.MaxExtractionBatches} extraction batches.");
        }

        var run = await _reviewRunTracker.StartAsync(
            document.DocumentId,
            request.InitiatedBy,
            cancellationToken);

        using var cancellationRegistration = _cancellationRegistry.Register(
            run.Id,
            cancellationToken);
        var reviewCancellationToken = cancellationRegistration.CancellationToken;

        await ReportProgressAsync(
            progressReporter,
            new LegalReviewProgressEvent(
                LegalReviewProgressEventType.Started,
                run.Id,
                BatchCount: batches.Count,
                InputItemCount: document.Chunks.Count,
                Message: "Legal review started."),
            reviewCancellationToken);

        _logger.LogInformation(
            "Legal review started. DocumentId: {DocumentId}; " +
            "ChunkCount: {ChunkCount}; BatchCount: {BatchCount}.",
            document.DocumentId,
            document.Chunks.Count,
            batches.Count);

        var extractedClauses = new List<ExtractedClause>();

        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];

            _logger.LogInformation(
                "Clause extraction batch started. DocumentId: {DocumentId}; " +
                "BatchNumber: {BatchNumber}; BatchCount: {BatchCount}; " +
                "SourceChunkCount: {SourceChunkCount}.",
                document.DocumentId,
                batchIndex + 1,
                batches.Count,
                batch.Length);

            ClauseExtractionResult extractionResult;
            await ReportProgressAsync(
                progressReporter,
                new LegalReviewProgressEvent(
                    LegalReviewProgressEventType.AgentStarted,
                    run.Id,
                    "ClauseExtractor",
                    batchIndex + 1,
                    batches.Count,
                    batch.Length,
                    Message: "Extracting clauses."),
                cancellationToken);

            var extractionStep = await _reviewRunTracker.StartStepAsync(
                run,
                "ClauseExtractor",
                batchIndex + 1,
                cancellationToken);

            try
            {
                extractionResult = await ExecuteWithRetryAsync(
                    "ClauseExtractor",
                    token => _clauseExtractorAgent.ExtractAsync(
                        new ClauseExtractionRequest(
                            document.DocumentId,
                            batch.ToList()),
                    token),
                    _policy.ClauseExtractorTimeoutSeconds,
                    reviewCancellationToken);
            }
            catch (OperationCanceledException)
                when (reviewCancellationToken.IsCancellationRequested)
            {
                await _reviewRunTracker.CancelStepAsync(
                    extractionStep,
                    "The legal review was cancelled.",
                    CancellationToken.None);
                return await CancelAsync(run, document.DocumentId, document.FileName);
            }
            catch (TimeoutException)
            {
                await _reviewRunTracker.FailStepAsync(extractionStep, "Agent attempt timed out.", CancellationToken.None);
                return await TerminateAsync(run,
                    document.DocumentId,
                    document.FileName,
                    ReviewTerminationReason.ClauseExtractorTimedOut,
                    "The Clause Extractor exceeded its time limit.");
            }
            catch (Exception exception)
            {
                await _reviewRunTracker.FailStepAsync(extractionStep, exception.Message, CancellationToken.None);
                return await TerminateAsync(run,
                    document.DocumentId,
                    document.FileName,
                    ReviewTerminationReason.ClauseExtractorFailed,
                    "The Clause Extractor could not complete after retry attempts.");
            }

            if (!extractionResult.Succeeded)
            {
                await _reviewRunTracker.FailStepAsync(
                    extractionStep,
                    extractionResult.FailureReason ?? "Invalid extraction result.",
                    cancellationToken);
                return await TerminateAsync(run,
                    document.DocumentId,
                    document.FileName,
                    ReviewTerminationReason.ClauseExtractorFailed,
                    extractionResult.FailureReason ??
                    "The Clause Extractor returned an invalid result.");
            }

            extractedClauses.AddRange(extractionResult.Clauses);

            await _reviewRunTracker.CompleteStepAsync(
                extractionStep,
                batch.Length,
                extractionResult.Clauses.Count,
                cancellationToken);

            await ReportProgressAsync(
                progressReporter,
                new LegalReviewProgressEvent(
                    LegalReviewProgressEventType.AgentCompleted,
                    run.Id,
                    "ClauseExtractor",
                    batchIndex + 1,
                    batches.Count,
                    batch.Length,
                    extractionResult.Clauses.Count,
                    "Clause extraction completed."),
                cancellationToken);

            _logger.LogInformation(
                "Clause extraction batch completed. DocumentId: {DocumentId}; " +
                "BatchNumber: {BatchNumber}; ExtractedClauseCount: " +
                "{ExtractedClauseCount}.",
                document.DocumentId,
                batchIndex + 1,
                extractionResult.Clauses.Count);
        }

        _logger.LogInformation(
            "Risk assessment started. DocumentId: {DocumentId}; " +
            "ExtractedClauseCount: {ExtractedClauseCount}.",
            document.DocumentId,
            extractedClauses.Count);

        RiskAssessmentResult riskAssessmentResult;
        await ReportProgressAsync(
            progressReporter,
            new LegalReviewProgressEvent(
                LegalReviewProgressEventType.AgentStarted,
                run.Id,
                "RiskAssessor",
                InputItemCount: extractedClauses.Count,
                Message: "Assessing risks."),
            cancellationToken);

        var riskAssessmentStep = await _reviewRunTracker.StartStepAsync(
            run,
            "RiskAssessor",
            batches.Count + 1,
            cancellationToken);

        try
        {
            riskAssessmentResult = await ExecuteWithRetryAsync(
                "RiskAssessor",
                token => _riskAssessorAgent.AssessAsync(
                    new RiskAssessmentRequest(
                        document.DocumentId,
                        document.FileName,
                        extractedClauses),
                    token),
                    _policy.RiskAssessorTimeoutSeconds,
                reviewCancellationToken);
        }
        catch (OperationCanceledException)
            when (reviewCancellationToken.IsCancellationRequested)
        {
            await _reviewRunTracker.CancelStepAsync(
                riskAssessmentStep,
                "The legal review was cancelled.",
                CancellationToken.None);
            return await CancelAsync(run, document.DocumentId, document.FileName);
        }
        catch (TimeoutException)
        {
            await _reviewRunTracker.FailStepAsync(riskAssessmentStep, "Agent attempt timed out.", CancellationToken.None);
            return await TerminateAsync(run,
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.RiskAssessorTimedOut,
                "The Risk Assessor exceeded its time limit.");
        }
        catch (Exception exception)
        {
            await _reviewRunTracker.FailStepAsync(riskAssessmentStep, exception.Message, CancellationToken.None);
            return await TerminateAsync(run,
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.RiskAssessorFailed,
                "The Risk Assessor could not complete after retry attempts.");
        }

        _logger.LogInformation(
            "Risk assessment completed. DocumentId: {DocumentId}; " +
            "RiskFindingCount: {RiskFindingCount}.",
            document.DocumentId,
            riskAssessmentResult.Findings.Count);

        await _reviewRunTracker.CompleteStepAsync(
            riskAssessmentStep,
            extractedClauses.Count,
            riskAssessmentResult.Findings.Count,
            cancellationToken);

        await ReportProgressAsync(
            progressReporter,
            new LegalReviewProgressEvent(
                LegalReviewProgressEventType.AgentCompleted,
                run.Id,
                "RiskAssessor",
                InputItemCount: extractedClauses.Count,
                OutputItemCount: riskAssessmentResult.Findings.Count,
                Message: "Risk assessment completed."),
            cancellationToken);

        _logger.LogInformation(
            "Memo drafting started. DocumentId: {DocumentId}; " +
            "RiskFindingCount: {RiskFindingCount}.",
            document.DocumentId,
            riskAssessmentResult.Findings.Count);

        MemoDraftResult memoDraftResult;
        await ReportProgressAsync(
            progressReporter,
            new LegalReviewProgressEvent(
                LegalReviewProgressEventType.AgentStarted,
                run.Id,
                "MemoDrafter",
                InputItemCount: riskAssessmentResult.Findings.Count,
                Message: "Drafting review memo."),
            cancellationToken);

        var memoDrafterStep = await _reviewRunTracker.StartStepAsync(
            run,
            "MemoDrafter",
            batches.Count + 2,
            cancellationToken);

        try
        {
            memoDraftResult = await ExecuteWithRetryAsync(
                "MemoDrafter",
                token => _memoDrafterAgent.DraftAsync(
                    new MemoDraftRequest(
                        document.DocumentId,
                        document.FileName,
                        document.Source,
                        document.Version,
                        extractedClauses,
                        riskAssessmentResult.Findings),
                    token),
                    _policy.MemoDrafterTimeoutSeconds,
                reviewCancellationToken);
        }
        catch (OperationCanceledException)
            when (reviewCancellationToken.IsCancellationRequested)
        {
            await _reviewRunTracker.CancelStepAsync(
                memoDrafterStep,
                "The legal review was cancelled.",
                CancellationToken.None);
            return await CancelAsync(run, document.DocumentId, document.FileName);
        }
        catch (TimeoutException)
        {
            await _reviewRunTracker.FailStepAsync(memoDrafterStep, "Agent attempt timed out.", CancellationToken.None);
            return await TerminateAsync(run,
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MemoDrafterTimedOut,
                "The Memo Drafter exceeded its time limit.");
        }
        catch (Exception exception)
        {
            await _reviewRunTracker.FailStepAsync(memoDrafterStep, exception.Message, CancellationToken.None);
            return await TerminateAsync(run,
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MemoDrafterFailed,
                "The Memo Drafter could not complete after retry attempts.");
        }

        if (!memoDraftResult.Succeeded ||
            string.IsNullOrWhiteSpace(memoDraftResult.Content))
        {
            await _reviewRunTracker.FailStepAsync(
                memoDrafterStep,
                memoDraftResult.FailureReason ?? "Invalid memo draft result.",
                cancellationToken);
            return await TerminateAsync(run,
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MemoDrafterFailed,
                memoDraftResult.FailureReason ??
                "The Memo Drafter returned an invalid result.");
        }

        var riskFindingDrafts = riskAssessmentResult.Findings
      .Select(finding => new ReviewMemoRiskFindingDraft(
          finding.RuleId,
          finding.ClauseType.ToString(),
          finding.Severity.ToString(),
          finding.Title,
          finding.Rationale,
          finding.Recommendation,
          finding.DocumentChunkId))
      .ToList();

        await _reviewRunTracker.CompleteStepAsync(
            memoDrafterStep,
            riskAssessmentResult.Findings.Count,
            memoDraftResult.CitationChunkIds.Count,
            cancellationToken);

        await ReportProgressAsync(
            progressReporter,
            new LegalReviewProgressEvent(
                LegalReviewProgressEventType.AgentCompleted,
                run.Id,
                "MemoDrafter",
                InputItemCount: riskAssessmentResult.Findings.Count,
                OutputItemCount: memoDraftResult.CitationChunkIds.Count,
                Message: "Review memo drafted."),
            cancellationToken);

        var citationChunkIds = memoDraftResult.CitationChunkIds
            .Concat(riskAssessmentResult.Findings
                .Where(finding => finding.DocumentChunkId.HasValue)
                .Select(finding => finding.DocumentChunkId!.Value))
            .Distinct()
            .ToList();

        var reviewMemo = ReviewMemo.CreateDraft(
            document.DocumentId,
            memoDraftResult.Content,
            citationChunkIds,
            riskFindingDrafts,
            DateTime.UtcNow);

        try
        {
            await _reviewMemoRepository.AddAsync(
                reviewMemo,
                cancellationToken);

            await _reviewMemoRepository.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Legal review completed. DocumentId: {DocumentId}; " +
                "ReviewMemoId: {ReviewMemoId}; ExtractedClauseCount: " +
                "{ExtractedClauseCount}; RiskFindingCount: {RiskFindingCount}.",
                document.DocumentId,
                reviewMemo.Id,
                extractedClauses.Count,
                riskAssessmentResult.Findings.Count);
        }
        catch (Exception)
        {
            return await TerminateAsync(run,
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MemoPersistenceFailed,
                "The memo draft could not be saved.");
        }

        var memoDraft = new ReviewMemoDraft(
            reviewMemo.Id,
            reviewMemo.Content,
            memoDraftResult.CitationChunkIds,
            reviewMemo.ApprovalStatus,
            reviewMemo.CreatedAtUtc);

        await _reviewRunTracker.CompleteAsync(run, reviewMemo.Id, cancellationToken);

        return new LegalReviewResult(
            LegalReviewStatus.Completed,
            document.DocumentId,
            document.FileName,
            extractedClauses,
            riskAssessmentResult.Findings,
            ReviewTerminationReason.None,
            null,
            memoDraft,
            run.Id);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        string agentName,
        Func<CancellationToken, Task<T>> action,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1;
             attempt <= _policy.AgentMaxAttempts;
             attempt++)
        {
            using var attemptTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            attemptTimeout.CancelAfter(
                TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                return await action(attemptTimeout.Token);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
                when (attemptTimeout.IsCancellationRequested)
            {
                lastException = new TimeoutException(
                    "The agent attempt timed out.");

                _logger.LogWarning(
                    "Agent attempt timed out. AgentName: {AgentName}; " +
                    "Attempt: {Attempt}; MaxAttempts: {MaxAttempts}; " +
                    "TimeoutSeconds: {TimeoutSeconds}.",
                    agentName,
                    attempt,
                    _policy.AgentMaxAttempts,
                    timeoutSeconds);

                if (attempt == _policy.AgentMaxAttempts)
                {
                    throw lastException;
                }
            }
            catch (Exception exception)
                when (IsTransientFailure(exception))
            {
                lastException = exception;

                _logger.LogWarning(
                    exception,
                    "Transient agent failure. AgentName: {AgentName}; " +
                    "Attempt: {Attempt}; MaxAttempts: {MaxAttempts}.",
                    agentName,
                    attempt,
                    _policy.AgentMaxAttempts);

                if (attempt == _policy.AgentMaxAttempts)
                {
                    throw;
                }
            }

            var delay = GetRetryDelay(attempt);

            await Task.Delay(delay, cancellationToken);
        }

        throw lastException ??
              new InvalidOperationException(
                  "The agent retry loop ended unexpectedly.");
    }

    private TimeSpan GetRetryDelay(int failedAttempt)
    {
        var multiplier = Math.Pow(2, failedAttempt - 1);

        return TimeSpan.FromMilliseconds(
            _policy.RetryBaseDelayMilliseconds * multiplier);
    }

    private static bool IsTransientFailure(Exception exception)
    {
        return exception is HttpRequestException ||
               exception is TimeoutException ||
               exception is TaskCanceledException;
    }

    private static ValueTask ReportProgressAsync(
        IReviewProgressReporter? progressReporter,
        LegalReviewProgressEvent progressEvent,
        CancellationToken cancellationToken) =>
        progressReporter is null
            ? ValueTask.CompletedTask
            : progressReporter.ReportAsync(progressEvent, cancellationToken);

   

    private LegalReviewResult Terminate(
        Guid documentId,
        string? fileName,
        ReviewTerminationReason reason,
        string message)
    {
        _logger.LogWarning(
            "Legal review terminated before a traceable run started. DocumentId: {DocumentId}; Reason: {TerminationReason}.",
            documentId,
            reason);

        return new LegalReviewResult(
            LegalReviewStatus.Terminated,
            documentId,
            fileName,
            Array.Empty<ExtractedClause>(),
            Array.Empty<RiskFinding>(),
            reason,
            message);
    }

    private async Task<LegalReviewResult> CancelAsync(
        ReviewRun run,
        Guid documentId,
        string fileName)
    {
        const string message = "The legal review was cancelled.";

        _logger.LogInformation(
            "Legal review cancelled. DocumentId: {DocumentId}; ReviewRunId: {ReviewRunId}.",
            documentId,
            run.Id);

        await _reviewRunTracker.CancelAsync(run, message, CancellationToken.None);

        return new LegalReviewResult(
            LegalReviewStatus.Cancelled,
            documentId,
            fileName,
            Array.Empty<ExtractedClause>(),
            Array.Empty<RiskFinding>(),
            ReviewTerminationReason.Cancelled,
            message,
            null,
            run.Id);
    }

    private async Task<LegalReviewResult> TerminateAsync(
        ReviewRun run,
        Guid documentId,
        string? fileName,
        ReviewTerminationReason reason,
        string message)
    {
        _logger.LogWarning(
            "Legal review terminated. DocumentId: {DocumentId}; " +
            "Reason: {TerminationReason}.",
            documentId,
            reason);

        await _reviewRunTracker.TerminateAsync(run, reason.ToString(), CancellationToken.None);

        return new LegalReviewResult(
            LegalReviewStatus.Terminated,
            documentId,
            fileName,
            Array.Empty<ExtractedClause>(),
            Array.Empty<RiskFinding>(),
            reason,
            message,
            null,
            run.Id);
    }
}
