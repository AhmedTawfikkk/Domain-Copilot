using DomainCopilot.Application.Documents.Answering;
using DomainCopilot.Application.Documents.Retrieval;
using DomainCopilot.Application.Providers;
using Moq;

namespace DomainCopilot.Application.Tests.Answering;

public class GroundedAnswerServiceTests
{
    [Fact]
    public async Task AnswerAsync_WhenValidCitedAnswer_ReturnsAnswerAndCitation()
    {
        var chunk = CreateChunk(
            "The supplier's liability is capped at fees paid.",
            "4.1 Limitation of Liability");

        var retrievalService = new Mock<IChunkRetrievalService>();

        retrievalService.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<RetreivalMode>(),
                It.IsAny<int>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chunk });

        var llmProvider = new Mock<ILlmProvider>();

        llmProvider.Setup(provider => provider.CompleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                $$"""
                {
                  "decision": "answer",
                  "answer": "The supplier's liability is capped at fees paid.",
                  "citationChunkIds": ["{{chunk.DocumentChunkId}}"],
                  "reason": null
                }
                """);

        var service = CreateService(
            retrievalService.Object,
            llmProvider.Object);

        var result = await service.AnswerAsync(
            new AnswerRequest("What is the liability cap?"));

        Assert.Equal(AnswerStatus.Answered, result.Status);
        Assert.Single(result.Citations);
        Assert.Equal(
            chunk.DocumentChunkId,
            result.Citations[0].DocumentChunkId);
    }

    [Fact]
    public async Task AnswerAsync_WhenNoChunksAreRetrieved_RefusesWithoutCallingLlm()
    {
        var retrievalService = new Mock<IChunkRetrievalService>();

        retrievalService.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<RetreivalMode>(),
                It.IsAny<int>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var llmProvider = new Mock<ILlmProvider>();

        var service = CreateService(
            retrievalService.Object,
            llmProvider.Object);

        var result = await service.AnswerAsync(
            new AnswerRequest("What is the governing law?"));

        Assert.Equal(AnswerStatus.Refused, result.Status);
        Assert.Empty(result.Citations);

        llmProvider.Verify(provider => provider.CompleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnswerAsync_WhenModelReferencesUnknownChunk_Refuses()
    {
        var retrievedChunk = CreateChunk(
            "Contract text.",
            "1. Scope");

        var unknownChunkId = Guid.NewGuid();

        var retrievalService = new Mock<IChunkRetrievalService>();

        retrievalService.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<RetreivalMode>(),
                It.IsAny<int>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { retrievedChunk });

        var llmProvider = new Mock<ILlmProvider>();

        llmProvider.Setup(provider => provider.CompleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                $$"""
                {
                  "decision": "answer",
                  "answer": "Unsupported answer.",
                  "citationChunkIds": ["{{unknownChunkId}}"],
                  "reason": null
                }
                """);

        var service = CreateService(
            retrievalService.Object,
            llmProvider.Object);

        var result = await service.AnswerAsync(
            new AnswerRequest("Question"));

        Assert.Equal(AnswerStatus.Refused, result.Status);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task AnswerAsync_WhenModelRefuses_ReturnsControlledRefusal()
    {
        var chunk = CreateChunk(
            "This contract does not contain a governing-law clause.",
            "Unknown");

        var retrievalService = new Mock<IChunkRetrievalService>();

        retrievalService.Setup(service => service.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<RetreivalMode>(),
                It.IsAny<int>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chunk });

        var llmProvider = new Mock<ILlmProvider>();

        llmProvider.Setup(provider => provider.CompleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                """
                {
                  "decision": "refuse",
                  "answer": "",
                  "citationChunkIds": [],
                  "reason": "No governing-law evidence was retrieved."
                }
                """);

        var service = CreateService(
            retrievalService.Object,
            llmProvider.Object);

        var result = await service.AnswerAsync(
            new AnswerRequest("What law governs the contract?"));

        Assert.Equal(AnswerStatus.Refused, result.Status);
        Assert.Empty(result.Citations);
        Assert.NotNull(result.RefusalReason);
    }

    private static GroundedAnswerService CreateService(
        IChunkRetrievalService retrievalService,
        ILlmProvider llmProvider)
    {
        var promptTemplate = new Mock<IGroundedAnswerPromptTemplate>();

        promptTemplate.Setup(template => template.Render(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievedChunk>>()))
            .Returns(new GroundedAnswerPrompts(
    SystemPrompt: "test system prompt",
    UserPrompt: "test user prompt"));

        return new GroundedAnswerService(
            retrievalService,
            llmProvider,
            promptTemplate.Object);
    }

    private static RetrievedChunk CreateChunk(
        string content,
        string clauseOrSection)
    {
        return new RetrievedChunk(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sample-contract.txt",
            "Test",
            "1.0",
            content,
            clauseOrSection,
            1,
            false,
            0.9);
    }
}