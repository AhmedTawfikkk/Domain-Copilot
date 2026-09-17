using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Enums;
using Moq;

namespace DomainCopilot.Application.Tests.Review;

public sealed class LegalReviewOrchestratorTests
{
    [Fact]
    public async Task ReviewAsync_WhenAgentsSucceed_ReturnsCompletedReview()
    {
        var document = CreateDocument();

        var repository = new Mock<IDocumentReviewRepository>();

        repository.Setup(item => item.GetDocumentAsync(
                document.DocumentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var extractor = new Mock<IClauseExtractorAgent>();

        extractor.Setup(item => item.ExtractAsync(
                It.IsAny<ClauseExtractionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClauseExtractionRequest request, CancellationToken _) =>
                new ClauseExtractionResult(
                    true,
                    request.Chunks.Select(chunk => new ExtractedClause(
                        chunk.DocumentChunkId,
                        LegalClauseType.LimitationOfLiability,
                        "Liability is capped.",
                        chunk.Content,
                        chunk.ClauseOrSection,
                        chunk.PageNumber,
                        chunk.LowConfidence,
                        0.90)).ToList(),
                    null));

        var assessor = new Mock<IRiskAssessorAgent>();

        assessor.Setup(item => item.AssessAsync(
                It.IsAny<RiskAssessmentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskAssessmentResult(
                Array.Empty<RiskFinding>()));

        var orchestrator = CreateOrchestrator(
            repository.Object,
            extractor.Object,
            assessor.Object);

        var result = await orchestrator.ReviewAsync(
            new LegalReviewRequest(document.DocumentId));

        Assert.Equal(LegalReviewStatus.Completed, result.Status);
        Assert.Equal(ReviewTerminationReason.None, result.TerminationReason);
        Assert.Equal(document.Chunks.Count, result.ExtractedClauses.Count);
        Assert.NotNull(result.MemoDraft);
        Assert.Equal(
            MemoApprovalStatus.Draft,
            result.MemoDraft.ApprovalStatus);

        extractor.Verify(item => item.ExtractAsync(
                It.IsAny<ClauseExtractionRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        assessor.Verify(item => item.AssessAsync(
                It.IsAny<RiskAssessmentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_WhenDocumentDoesNotExist_TerminatesWithoutCallingAgents()
    {
        var documentId = Guid.NewGuid();

        var repository = new Mock<IDocumentReviewRepository>();

        repository.Setup(item => item.GetDocumentAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewDocument?)null);

        var extractor = new Mock<IClauseExtractorAgent>();
        var assessor = new Mock<IRiskAssessorAgent>();

        var orchestrator = CreateOrchestrator(
            repository.Object,
            extractor.Object,
            assessor.Object);

        var result = await orchestrator.ReviewAsync(
            new LegalReviewRequest(documentId));

        Assert.Equal(LegalReviewStatus.Terminated, result.Status);
        Assert.Equal(
            ReviewTerminationReason.DocumentNotFound,
            result.TerminationReason);

        extractor.Verify(item => item.ExtractAsync(
                It.IsAny<ClauseExtractionRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        assessor.Verify(item => item.AssessAsync(
                It.IsAny<RiskAssessmentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReviewAsync_WhenClauseExtractorFails_DoesNotCallRiskAssessor()
    {
        var document = CreateDocument();

        var repository = new Mock<IDocumentReviewRepository>();

        repository.Setup(item => item.GetDocumentAsync(
                document.DocumentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var extractor = new Mock<IClauseExtractorAgent>();

        extractor.Setup(item => item.ExtractAsync(
                It.IsAny<ClauseExtractionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClauseExtractionResult(
                false,
                Array.Empty<ExtractedClause>(),
                "The Clause Extractor returned invalid JSON."));

        var assessor = new Mock<IRiskAssessorAgent>();

        var orchestrator = CreateOrchestrator(
            repository.Object,
            extractor.Object,
            assessor.Object);

        var result = await orchestrator.ReviewAsync(
            new LegalReviewRequest(document.DocumentId));

        Assert.Equal(LegalReviewStatus.Terminated, result.Status);
        Assert.Equal(
            ReviewTerminationReason.ClauseExtractorFailed,
            result.TerminationReason);

        assessor.Verify(item => item.AssessAsync(
                It.IsAny<RiskAssessmentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReviewAsync_WhenDocumentExceedsChunkLimit_TerminatesWithoutCallingAgents()
    {
        var chunks = Enumerable.Range(1, 101)
            .Select(index => new ReviewSourceChunk(
                Guid.NewGuid(),
                $"Clause {index}",
                $"Section {index}",
                index,
                false,
                index))
            .ToList();

        var document = new ReviewDocument(
            Guid.NewGuid(),
            "large-contract.txt",
            "Test",
            "1.0",
            DocumentStatus.Chunked,
            chunks);

        var repository = new Mock<IDocumentReviewRepository>();

        repository.Setup(item => item.GetDocumentAsync(
                document.DocumentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var extractor = new Mock<IClauseExtractorAgent>();
        var assessor = new Mock<IRiskAssessorAgent>();

        var orchestrator = CreateOrchestrator(
            repository.Object,
            extractor.Object,
            assessor.Object);

        var result = await orchestrator.ReviewAsync(
            new LegalReviewRequest(document.DocumentId));

        Assert.Equal(LegalReviewStatus.Terminated, result.Status);
        Assert.Equal(
            ReviewTerminationReason.MaximumChunkCountExceeded,
            result.TerminationReason);

        extractor.Verify(item => item.ExtractAsync(
                It.IsAny<ClauseExtractionRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static LegalReviewOrchestrator CreateOrchestrator(
     IDocumentReviewRepository repository,
     IClauseExtractorAgent extractor,
     IRiskAssessorAgent assessor)
    {
        var memoDrafter = new Mock<IMemoDrafterAgent>();

        memoDrafter.Setup(item => item.DraftAsync(
                It.IsAny<MemoDraftRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (MemoDraftRequest request, CancellationToken _) =>
                    new MemoDraftResult(
                        true,
                        "## Executive Summary\n\nDraft memo for counsel review.",
                        new[]
                        {
                        request.ExtractedClauses
                            .First()
                            .DocumentChunkId
                        },
                        null));

        var memoRepository = new Mock<IReviewMemoRepository>();

        memoRepository.Setup(item => item.AddAsync(
                It.IsAny<DomainCopilot.Domain.Entites.ReviewMemo>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        memoRepository.Setup(item => item.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new LegalReviewOrchestrator(
            repository,
            extractor,
            assessor,
            memoDrafter.Object,
            memoRepository.Object,
            new ReviewExecutionPolicy
            {
                MaxChunksPerReview = 100,
                MaxChunksPerExtractionBatch = 10,
                MaxExtractionBatches = 10,
                MaxCharactersPerChunk = 1_500,
                ClauseExtractorTimeoutSeconds = 45,
                MemoDrafterTimeoutSeconds = 45,
                RiskAssessorTimeoutSeconds = 5,
                AgentMaxAttempts = 3,
                RetryBaseDelayMilliseconds = 100
            });
    }

    private static ReviewDocument CreateDocument()
    {
        var chunk = new ReviewSourceChunk(
            Guid.NewGuid(),
            "The supplier's aggregate liability shall not exceed fees paid.",
            "12. Limitation of Liability",
            1,
            false,
            0);

        return new ReviewDocument(
            Guid.NewGuid(),
            "sample-contract.txt",
            "Test",
            "1.0",
            DocumentStatus.Chunked,
            new[] { chunk });
    }
}
