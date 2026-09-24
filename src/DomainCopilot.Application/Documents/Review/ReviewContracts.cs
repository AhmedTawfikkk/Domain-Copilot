using DomainCopilot.Domain.Enums;
using DomainCopilot.Domain.Entites;
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
        Terminated = 1,
        Cancelled = 2
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
        RiskAssessorFailed = 10,
        MemoDrafterTimedOut = 11,
        MemoDrafterFailed = 12,
        MemoPersistenceFailed = 13,
        Cancelled = 14
    }

    public sealed record LegalReviewRequest(
        Guid DocumentId,
        string InitiatedBy = "system");

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
      string? TerminationMessage,
      ReviewMemoDraft? MemoDraft = null,
      Guid ReviewRunId = default);

    public enum LegalReviewProgressEventType
    {
        Started = 0,
        AgentStarted = 1,
        AgentCompleted = 2,
        Completed = 3,
        Terminated = 4,
        Cancelled = 5,
        Error = 6
    }

    public sealed record LegalReviewProgressEvent(
        LegalReviewProgressEventType Type,
        Guid? ReviewRunId,
        string? AgentName = null,
        int? BatchNumber = null,
        int? BatchCount = null,
        int? InputItemCount = null,
        int? OutputItemCount = null,
        string? Message = null,
        LegalReviewResult? Result = null);

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
        public int MemoDrafterTimeoutSeconds { get; init; } = 45;

        public int AgentMaxAttempts { get; init; } = 3;

        public int RetryBaseDelayMilliseconds { get; init; } = 1_000;

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
            if (MemoDrafterTimeoutSeconds is < 5 or > 120)
            {
                throw new InvalidOperationException(
                    "MemoDrafterTimeoutSeconds must be between 5 and 120.");
            }

            if (AgentMaxAttempts is < 1 or > 5)
            {
                throw new InvalidOperationException(
                    "AgentMaxAttempts must be between 1 and 5.");
            }

            if (RetryBaseDelayMilliseconds is < 100 or > 5_000)
            {
                throw new InvalidOperationException(
                    "RetryBaseDelayMilliseconds must be between 100 and 5000.");
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
            CancellationToken cancellationToken = default,
            IReviewProgressReporter? progressReporter = null);
    }

    public interface IReviewProgressReporter
    {
        ValueTask ReportAsync(
            LegalReviewProgressEvent progressEvent,
            CancellationToken cancellationToken = default);
    }

    public interface IStreamingLegalReviewService
    {
        IAsyncEnumerable<LegalReviewProgressEvent> StreamAsync(
            LegalReviewRequest request,
            CancellationToken cancellationToken = default);
    }

    public interface IReviewRunCancellationRegistry
    {
        IReviewRunCancellationRegistration Register(
            Guid reviewRunId,
            CancellationToken requestCancellationToken = default);

        bool TryCancel(Guid reviewRunId);
    }

    public interface IReviewRunCancellationRegistration : IDisposable
    {
        CancellationToken CancellationToken { get; }
    }

    public sealed record ReviewRunSummary(
        Guid Id,
        Guid DocumentId,
        string FileName,
        string InitiatedBy,
        string? CorrelationId,
        string Status,
        DateTime StartedAtUtc,
        DateTime? CompletedAtUtc,
        Guid? ReviewMemoId,
        string? TerminationReason);

    public sealed record ReviewAgentStepTrace(
        Guid Id,
        string AgentName,
        int Sequence,
        string Status,
        int? InputItemCount,
        int? OutputItemCount,
        string? FailureReason,
        DateTime StartedAtUtc,
        DateTime? CompletedAtUtc);

    public sealed record LlmCallTrace(
        string Provider,
        string Model,
        string Operation,
        int? InputTokens,
        int? OutputTokens,
        int? TotalTokens,
        decimal? EstimatedCostUsd,
        bool Succeeded,
        bool WasCancelled,
        string? FailureReason,
        long DurationMilliseconds,
        DateTime CreatedAtUtc);

    public sealed record ReviewRunTrace(
        ReviewRunSummary Run,
        IReadOnlyList<ReviewAgentStepTrace> Steps,
        IReadOnlyList<LlmCallTrace> LlmCalls);

    public interface IReviewRunRepository
    {
        Task AddAsync(ReviewRun run, CancellationToken cancellationToken = default);
        Task AddStepAsync(ReviewAgentStep step, CancellationToken cancellationToken = default);
        Task<ReviewRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
        Task<ReviewRunTrace?> GetTraceAsync(Guid runId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<LlmCallTrace>> GetLlmCallsAsync(
            string? correlationId,
            int limit,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ReviewRunSummary>> ListAsync(
            Guid? ownerScopeId = null,
            CancellationToken cancellationToken = default);
        Task<Guid?> GetDocumentIdAsync(Guid runId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }

    public interface IReviewRunTracker
    {
        Task<ReviewRun> StartAsync(Guid documentId, string initiatedBy, CancellationToken cancellationToken = default);
        Task<ReviewAgentStep> StartStepAsync(ReviewRun run, string agentName, int sequence, CancellationToken cancellationToken = default);
        Task CompleteStepAsync(ReviewAgentStep step, int inputItemCount, int outputItemCount, CancellationToken cancellationToken = default);
        Task FailStepAsync(ReviewAgentStep step, string reason, CancellationToken cancellationToken = default);
        Task CancelStepAsync(ReviewAgentStep step, string reason, CancellationToken cancellationToken = default);
        Task CompleteAsync(ReviewRun run, Guid reviewMemoId, CancellationToken cancellationToken = default);
        Task TerminateAsync(ReviewRun run, string reason, CancellationToken cancellationToken = default);
        Task CancelAsync(ReviewRun run, string reason, CancellationToken cancellationToken = default);
    }
}
