using DomainCopilot.Application.Documents.Answering;
using DomainCopilot.Application.Documents.Evaluation;
using Moq;

namespace DomainCopilot.Application.Tests.Evaluation;

public class EvaluationHarnessTests
{
    [Fact]
    public async Task RunAsync_WhenAnswerAndRefusalMatchExpectations_CalculatesMetrics()
    {
        var answerService = new Mock<IGroundedAnswerService>();

        var liabilityCitation = new Citation(
            Guid.NewGuid(),
            "master-services-agreement.txt",
            "Dataset",
            "1.0",
            "SECTION 7 LIMITATION OF LIABILITY",
            4,
            false);

        answerService.Setup(service => service.AnswerAsync(
                It.Is<AnswerRequest>(request =>
                    request.Question == "What is the liability cap?"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedAnswerResult(
                AnswerStatus.Answered,
                "The liability cap is fees paid.",
                new[] { liabilityCitation },
                null));

        answerService.Setup(service => service.AnswerAsync(
                It.Is<AnswerRequest>(request =>
                    request.Question == "What is the CEO's home address?"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedAnswerResult(
                AnswerStatus.Refused,
                "I do not have sufficient grounded evidence.",
                Array.Empty<Citation>(),
                "No relevant source chunks were retrieved."));

        var harness = new EvaluationHarness(
            answerService.Object);

        var cases = new[]
        {
            new GoldenEvaluationCase(
                "E-01",
                EvaluationScenario.Baseline,
                "What is the liability cap?",
                new[] { AnswerStatus.Answered },
                new[] { "LIMITATION OF LIABILITY" }),

            new GoldenEvaluationCase(
                "E-02",
                EvaluationScenario.OutOfCorpus,
                "What is the CEO's home address?",
                new[] { AnswerStatus.Refused },
                Array.Empty<string>())
        };

        var result = await harness.RunAsync(cases);

        Assert.Equal(2, result.Metrics.TotalCases);
        Assert.Equal(2, result.Metrics.CompletedCases);
        Assert.Equal(0, result.Metrics.FailedCases);

        Assert.Equal(2, result.Metrics.OutcomeMatches);
        Assert.Equal(1m, result.Metrics.OutcomeMatchRate);

        Assert.Equal(1, result.Metrics.StrictAnswerCases);
        Assert.Equal(1, result.Metrics.AnswerHits);
        Assert.Equal(1m, result.Metrics.AnswerHitRate);

        Assert.Equal(1, result.Metrics.AnsweredCases);
        Assert.Equal(1, result.Metrics.GroundedAnsweredCases);
        Assert.Equal(1m, result.Metrics.GroundednessRate);

        Assert.Equal(1, result.Metrics.StrictRefusalCases);
        Assert.Equal(1, result.Metrics.CorrectRefusals);
        Assert.Equal(1m, result.Metrics.RefusalCorrectnessRate);
    }

    [Fact]
    public async Task RunAsync_WhenAnsweredResponseHasWrongSection_DoesNotCountAnswerHit()
    {
        var answerService = new Mock<IGroundedAnswerService>();

        var wrongCitation = new Citation(
            Guid.NewGuid(),
            "master-services-agreement.txt",
            "Dataset",
            "1.0",
            "SECTION 3 CONFIDENTIALITY",
            2,
            false);

        answerService.Setup(service => service.AnswerAsync(
                It.IsAny<AnswerRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedAnswerResult(
                AnswerStatus.Answered,
                "An answer with a citation.",
                new[] { wrongCitation },
                null));

        var harness = new EvaluationHarness(
            answerService.Object);

        var cases = new[]
        {
            new GoldenEvaluationCase(
                "E-03",
                EvaluationScenario.Baseline,
                "What is the liability cap?",
                new[] { AnswerStatus.Answered },
                new[] { "LIMITATION OF LIABILITY" })
        };

        var result = await harness.RunAsync(cases);

        Assert.True(result.Cases[0].OutcomeMatched);
        Assert.False(result.Cases[0].CitationMatched);

        Assert.Equal(0, result.Metrics.AnswerHits);
        Assert.Equal(0m, result.Metrics.AnswerHitRate);
        Assert.Equal(1m, result.Metrics.GroundednessRate);
    }

    [Fact]
    public async Task RunAsync_WhenCaseIdsAreDuplicated_ThrowsArgumentException()
    {
        var answerService = new Mock<IGroundedAnswerService>();

        var harness = new EvaluationHarness(
            answerService.Object);

        var cases = new[]
        {
            new GoldenEvaluationCase(
                "E-04",
                EvaluationScenario.Baseline,
                "Question one",
                new[] { AnswerStatus.Refused },
                Array.Empty<string>()),

            new GoldenEvaluationCase(
                "E-04",
                EvaluationScenario.OutOfCorpus,
                "Question two",
                new[] { AnswerStatus.Refused },
                Array.Empty<string>())
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => harness.RunAsync(cases));

        Assert.Contains(
            "Duplicate evaluation case ID",
            exception.Message);
    }
}