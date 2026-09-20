using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace DomainCopilot.Application.Tests.Review;

public sealed class MemoDrafterAgentTests
{
    [Fact]
    public async Task DraftAsync_DerivesCitationsFromTrustedRiskFindings()
    {
        var chunkId = Guid.NewGuid();
        var provider = new Mock<ILlmProvider>();
        var promptTemplate = new Mock<IMemoDraftPromptTemplate>();

        provider
            .Setup(item => item.CompleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {
                  "memoMarkdown": "## Executive Summary\nA risk requires counsel review.",
                  "citationChunkIds": ["not-a-guid"]
                }
                """);

        promptTemplate
            .Setup(item => item.Render(It.IsAny<MemoDraftRequest>()))
            .Returns(new MemoDraftPrompts("system", "user"));

        var agent = new MemoDrafterAgent(
            provider.Object,
            promptTemplate.Object,
            Mock.Of<ILogger<MemoDrafterAgent>>());

        var result = await agent.DraftAsync(new MemoDraftRequest(
            Guid.NewGuid(),
            "sample-agreement.pdf",
            "Dataset",
            "1.0",
            new[]
            {
                new ExtractedClause(
                    chunkId,
                    LegalClauseType.Payment,
                    "Payment terms are stated.",
                    "Payment is due within thirty days.",
                    "SECTION 2 PAYMENT",
                    1,
                    false,
                    0.95)
            },
            new[]
            {
                new RiskFinding(
                    "PB-PAY-001",
                    LegalClauseType.Payment,
                    RiskSeverity.Medium,
                    "Payment concern",
                    "The playbook requires a defined payment term.",
                    "Confirm the payment term.",
                    chunkId)
            }));

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { chunkId }, result.CitationChunkIds);
    }
}
