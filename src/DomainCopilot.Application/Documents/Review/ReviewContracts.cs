using DomainCopilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Application.Documents.Review
{
    public enum LegalClauseType
    {
        Unknown = 0,
        LimitationOfLiability = 1,
        Indemnification = 2,
        Confidentiality = 3,
        Termination = 4,
        GoverningLaw = 5,
        DataProtection = 6,
        IntellectualProperty = 7,
        Payment = 8,
        Other = 9
    }

    public enum RiskSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum LegalReviewStatus
    {
        Completed = 0,
        Terminated = 1
    }

    public enum ReviewTerminationReason
    {
        None = 0,
        InvalidRequest = 1,
        DocumentNotFound = 2,
        DocumentFailed = 3,
        DocumentHasNoChunks = 4,
        MaximumChunkCountExceeded = 5,
        MaximumExtractionBatchCountExceeded = 6,
        ClauseExtractorTimedOut = 7,
        ClauseExtractorFailed = 8,
        RiskAssessorTimedOut = 9,
        RiskAssessorFailed = 10
    }

    public sealed record LegalReviewRequest(
        Guid DocumentId);

    public sealed record ReviewDocument(
        Guid DocumentId,
        string FileName,
        string Source,
        string Version,
        DocumentStatus Status,
        IReadOnlyList<ReviewSourceChunk> Chunks);

    public sealed record ReviewSourceChunk(
        Guid DocumentChunkId,
        string Content,
        string? ClauseOrSection,
        int? PageNumber,
        bool LowConfidence,
        int ChunkIndex);

    public sealed record ClauseExtractionRequest(
        Guid DocumentId,
        IReadOnlyList<ReviewSourceChunk> Chunks);

    public sealed record ExtractedClause(
        Guid DocumentChunkId,
        LegalClauseType ClauseType,
        string Summary,
        string EvidenceText,
        string? ClauseOrSection,
        int? PageNumber,
        bool LowConfidence,
        double Confidence);

    public sealed record ClauseExtractionResult(
        bool Succeeded,
        IReadOnlyList<ExtractedClause> Clauses,
        string? FailureReason);

    public sealed record RiskAssessmentRequest(
        Guid DocumentId,
        string FileName,
        IReadOnlyList<ExtractedClause> Clauses);

    public sealed record RiskFinding(
        string RuleId,
        LegalClauseType ClauseType,
        RiskSeverity Severity,
        string Title,
        string Rationale,
        string Recommendation,
        Guid? DocumentChunkId);

    public sealed record RiskAssessmentResult(
        IReadOnlyList<RiskFinding> Findings);

    public sealed record LegalReviewResult(
        LegalReviewStatus Status,
        Guid DocumentId,
        string? FileName,
        IReadOnlyList<ExtractedClause> ExtractedClauses,
        IReadOnlyList<RiskFinding> RiskFindings,
        ReviewTerminationReason TerminationReason,
        string? TerminationMessage);

    public sealed record ClauseExtractionPrompts(
        string SystemPrompt,
        string UserPrompt);

    public sealed class ReviewExecutionPolicy
    {
        public int MaxChunksPerReview { get; init; } = 100;

        public int MaxChunksPerExtractionBatch { get; init; } = 10;

        public int MaxExtractionBatches { get; init; } = 10;

        public int MaxCharactersPerChunk { get; init; } = 1_500;

        public int ClauseExtractorTimeoutSeconds { get; init; } = 45;

        public int RiskAssessorTimeoutSeconds { get; init; } = 5;

        public void Validate()
        {
            if (MaxChunksPerReview is < 1 or > 200)
            {
                throw new InvalidOperationException(
                    "MaxChunksPerReview must be between 1 and 200.");
            }

            if (MaxChunksPerExtractionBatch is < 1 or > 20)
            {
                throw new InvalidOperationException(
                    "MaxChunksPerExtractionBatch must be between 1 and 20.");
            }

            if (MaxExtractionBatches is < 1 or > 20)
            {
                throw new InvalidOperationException(
                    "MaxExtractionBatches must be between 1 and 20.");
            }

            if (MaxCharactersPerChunk is < 200 or > 5_000)
            {
                throw new InvalidOperationException(
                    "MaxCharactersPerChunk must be between 200 and 5000.");
            }

            if (ClauseExtractorTimeoutSeconds is < 5 or > 120)
            {
                throw new InvalidOperationException(
                    "ClauseExtractorTimeoutSeconds must be between 5 and 120.");
            }

            if (RiskAssessorTimeoutSeconds is < 1 or > 30)
            {
                throw new InvalidOperationException(
                    "RiskAssessorTimeoutSeconds must be between 1 and 30.");
            }
        }
    }

    public interface IDocumentReviewRepository
    {
        Task<ReviewDocument?> GetDocumentAsync(
            Guid documentId,
            CancellationToken cancellationToken = default);
    }

    public interface IClauseExtractionPromptTemplate
    {
        string Version { get; }

        ClauseExtractionPrompts Render(
            IReadOnlyList<ReviewSourceChunk> chunks,
            int maximumChunkCharacters);
    }

    public interface IClauseExtractorAgent
    {
        Task<ClauseExtractionResult> ExtractAsync(
            ClauseExtractionRequest request,
            CancellationToken cancellationToken = default);
    }

    public interface IContractReviewPlaybook
    {
        RiskAssessmentResult Assess(RiskAssessmentRequest request);
    }

    public interface IRiskAssessorAgent
    {
        Task<RiskAssessmentResult> AssessAsync(
            RiskAssessmentRequest request,
            CancellationToken cancellationToken = default);
    }

    public interface ILegalReviewOrchestrator
    {
        Task<LegalReviewResult> ReviewAsync(
            LegalReviewRequest request,
            CancellationToken cancellationToken = default);
    }
}
