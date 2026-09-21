using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace DomainCopilot.Application.Tests.Review;

public sealed class StreamingLegalReviewServiceTests
{
    [Fact]
    public async Task StreamAsync_WhenReviewCompletes_EmitsOrderedProgressAndCompletedEvents()
    {
        var request = new LegalReviewRequest(Guid.NewGuid(), "lawyer@example.com");
        var runId = Guid.NewGuid();
        var orchestrator = new Mock<ILegalReviewOrchestrator>();

        orchestrator.Setup(item => item.ReviewAsync(
                request,
                It.IsAny<CancellationToken>(),
                It.IsAny<IReviewProgressReporter>()))
            .Returns(async (
                LegalReviewRequest _,
                CancellationToken cancellationToken,
                IReviewProgressReporter progressReporter) =>
            {
                await progressReporter.ReportAsync(
                    new LegalReviewProgressEvent(
                        LegalReviewProgressEventType.Started,
                        runId,
                        BatchCount: 1,
                        InputItemCount: 2,
                        Message: "Legal review started."),
                    cancellationToken);

                await progressReporter.ReportAsync(
                    new LegalReviewProgressEvent(
                        LegalReviewProgressEventType.AgentStarted,
                        runId,
                        "ClauseExtractor",
                        1,
                        1,
                        2,
                        Message: "Extracting clauses."),
                    cancellationToken);

                return new LegalReviewResult(
                    LegalReviewStatus.Completed,
                    request.DocumentId,
                    "sample-contract.txt",
                    Array.Empty<ExtractedClause>(),
                    Array.Empty<RiskFinding>(),
                    ReviewTerminationReason.None,
                    null,
                    ReviewRunId: runId);
            });

        var service = new StreamingLegalReviewService(
            orchestrator.Object,
            Mock.Of<ILogger<StreamingLegalReviewService>>());

        var events = new List<LegalReviewProgressEvent>();

        await foreach (var progressEvent in service.StreamAsync(request))
        {
            events.Add(progressEvent);
        }

        Assert.Collection(events,
            progressEvent =>
            {
                Assert.Equal(LegalReviewProgressEventType.Started, progressEvent.Type);
                Assert.Equal(runId, progressEvent.ReviewRunId);
            },
            progressEvent =>
            {
                Assert.Equal(LegalReviewProgressEventType.AgentStarted, progressEvent.Type);
                Assert.Equal("ClauseExtractor", progressEvent.AgentName);
            },
            progressEvent =>
            {
                Assert.Equal(LegalReviewProgressEventType.Completed, progressEvent.Type);
                Assert.Equal(runId, progressEvent.ReviewRunId);
                Assert.NotNull(progressEvent.Result);
            });
    }
}
