namespace DomainCopilot.Application.Documents.Review;

public sealed class LegalReviewOrchestrator : ILegalReviewOrchestrator
{
    private readonly IDocumentReviewRepository _documentReviewRepository;
    private readonly IClauseExtractorAgent _clauseExtractorAgent;
    private readonly IRiskAssessorAgent _riskAssessorAgent;
    private readonly ReviewExecutionPolicy _policy;

    public LegalReviewOrchestrator(
        IDocumentReviewRepository documentReviewRepository,
        IClauseExtractorAgent clauseExtractorAgent,
        IRiskAssessorAgent riskAssessorAgent,
        ReviewExecutionPolicy policy)
    {
        _documentReviewRepository = documentReviewRepository;
        _clauseExtractorAgent = clauseExtractorAgent;
        _riskAssessorAgent = riskAssessorAgent;
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

        if (document.Status == Domain.Enums.DocumentStatus.Failed)
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

        var extractedClauses = new List<ExtractedClause>();
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

        foreach (var batch in batches)
        {
            ClauseExtractionResult extractionResult;

            using var extractorTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            extractorTimeout.CancelAfter(
                TimeSpan.FromSeconds(
                    _policy.ClauseExtractorTimeoutSeconds));

            try
            {
                extractionResult = await _clauseExtractorAgent.ExtractAsync(
                    new ClauseExtractionRequest(
                        document.DocumentId,
                        batch.ToList()),
                    extractorTimeout.Token);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
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
                    "The Clause Extractor could not complete the review.");
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

        using var riskTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        riskTimeout.CancelAfter(
            TimeSpan.FromSeconds(
                _policy.RiskAssessorTimeoutSeconds));

        try
        {
            riskAssessmentResult = await _riskAssessorAgent.AssessAsync(
                new RiskAssessmentRequest(
                    document.DocumentId,
                    document.FileName,
                    extractedClauses),
                riskTimeout.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
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
                "The Risk Assessor could not complete the review.");
        }

        return new LegalReviewResult(
            LegalReviewStatus.Completed,
            document.DocumentId,
            document.FileName,
            extractedClauses,
            riskAssessmentResult.Findings,
            ReviewTerminationReason.None,
            null);
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