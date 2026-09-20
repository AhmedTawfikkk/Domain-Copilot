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
    private readonly ReviewExecutionPolicy _policy;
    private readonly ILogger<LegalReviewOrchestrator> _logger;

    public LegalReviewOrchestrator(
        IDocumentReviewRepository documentReviewRepository,
        IClauseExtractorAgent clauseExtractorAgent,
        IRiskAssessorAgent riskAssessorAgent,
        IMemoDrafterAgent memoDrafterAgent,
        IReviewMemoRepository reviewMemoRepository,
        ReviewExecutionPolicy policy,
        ILogger<LegalReviewOrchestrator> logger)
    {
        _documentReviewRepository = documentReviewRepository;
        _clauseExtractorAgent = clauseExtractorAgent;
        _riskAssessorAgent = riskAssessorAgent;
        _memoDrafterAgent = memoDrafterAgent;
        _reviewMemoRepository = reviewMemoRepository;
        _policy = policy;
        _logger = logger;
    }

    public async Task<LegalReviewResult> ReviewAsync(
        LegalReviewRequest request,
        CancellationToken cancellationToken = default)
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
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                return Terminate(
                    document.DocumentId,
                    document.FileName,
                    ReviewTerminationReason.ClauseExtractorTimedOut,
                    "The Clause Extractor exceeded its time limit.");
            }
            catch (Exception)
            {
                return Terminate(
                    document.DocumentId,
                    document.FileName,
                    ReviewTerminationReason.ClauseExtractorFailed,
                    "The Clause Extractor could not complete after retry attempts.");
            }

            if (!extractionResult.Succeeded)
            {
                return Terminate(
                    document.DocumentId,
                    document.FileName,
                    ReviewTerminationReason.ClauseExtractorFailed,
                    extractionResult.FailureReason ??
                    "The Clause Extractor returned an invalid result.");
            }

            extractedClauses.AddRange(extractionResult.Clauses);

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
                cancellationToken);
        }
        catch (TimeoutException)
        {
            return Terminate(
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.RiskAssessorTimedOut,
                "The Risk Assessor exceeded its time limit.");
        }
        catch (Exception)
        {
            return Terminate(
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

        _logger.LogInformation(
            "Memo drafting started. DocumentId: {DocumentId}; " +
            "RiskFindingCount: {RiskFindingCount}.",
            document.DocumentId,
            riskAssessmentResult.Findings.Count);

        MemoDraftResult memoDraftResult;

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
                cancellationToken);
        }
        catch (TimeoutException)
        {
            return Terminate(
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MemoDrafterTimedOut,
                "The Memo Drafter exceeded its time limit.");
        }
        catch (Exception)
        {
            return Terminate(
                document.DocumentId,
                document.FileName,
                ReviewTerminationReason.MemoDrafterFailed,
                "The Memo Drafter could not complete after retry attempts.");
        }

        if (!memoDraftResult.Succeeded ||
            string.IsNullOrWhiteSpace(memoDraftResult.Content))
        {
            return Terminate(
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
            return Terminate(
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

        return new LegalReviewResult(
            LegalReviewStatus.Completed,
            document.DocumentId,
            document.FileName,
            extractedClauses,
            riskAssessmentResult.Findings,
            ReviewTerminationReason.None,
            null,
            memoDraft);
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

   

    private LegalReviewResult Terminate(
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

        return new LegalReviewResult(
            LegalReviewStatus.Terminated,
            documentId,
            fileName,
            Array.Empty<ExtractedClause>(),
            Array.Empty<RiskFinding>(),
            reason,
            message);
    }
}
