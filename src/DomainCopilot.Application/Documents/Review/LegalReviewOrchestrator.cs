using System.Diagnostics;
using DomainCopilot.Domain.Entites;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Application.Documents.Review;

public sealed class LegalReviewOrchestrator : ILegalReviewOrchestrator
{
    private readonly IDocumentReviewRepository _documentReviewRepository;
    private readonly IClauseExtractorAgent _clauseExtractorAgent;
    private readonly IRiskAssessorAgent _riskAssessorAgent;
    private readonly IMemoDrafterAgent _memoDrafterAgent;
    private readonly IReviewMemoRepository _reviewMemoRepository;
    private readonly ReviewExecutionPolicy _policy;

    public LegalReviewOrchestrator(
        IDocumentReviewRepository documentReviewRepository,
        IClauseExtractorAgent clauseExtractorAgent,
        IRiskAssessorAgent riskAssessorAgent,
        IMemoDrafterAgent memoDrafterAgent,
        IReviewMemoRepository reviewMemoRepository,
        ReviewExecutionPolicy policy)
    {
        _documentReviewRepository = documentReviewRepository;
        _clauseExtractorAgent = clauseExtractorAgent;
        _riskAssessorAgent = riskAssessorAgent;
        _memoDrafterAgent = memoDrafterAgent;
        _reviewMemoRepository = reviewMemoRepository;
        _policy = policy;
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

        var extractedClauses = new List<ExtractedClause>();

        foreach (var batch in batches)
        {
            ClauseExtractionResult extractionResult;

            try
            {
                extractionResult = await ExecuteWithRetryAsync(
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
        }

        RiskAssessmentResult riskAssessmentResult;

        try
        {
            riskAssessmentResult = await ExecuteWithRetryAsync(
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

        MemoDraftResult memoDraftResult;

        try
        {
            memoDraftResult = await ExecuteWithRetryAsync(
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

                if (attempt == _policy.AgentMaxAttempts)
                {
                    throw lastException;
                }
            }
            catch (Exception exception)
                when (IsTransientFailure(exception))
            {
                lastException = exception;

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

   

    private static LegalReviewResult Terminate(
        Guid documentId,
        string? fileName,
        ReviewTerminationReason reason,
        string message)
    {
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