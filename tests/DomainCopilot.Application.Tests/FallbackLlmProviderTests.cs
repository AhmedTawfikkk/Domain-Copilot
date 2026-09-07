using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DomainCopilot.Application.Tests;

public class FallbackLlmProviderTests
{
    [Fact]
    public async Task CompleteAsync_WhenPrimarySucceeds_ReturnsPrimaryResult()
    {
        var primary = new Mock<ILlmProvider>();
        primary.Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("primary response");
        var fallback = new Mock<ILlmProvider>();
        var logger = new Mock<ILogger<FallbackLlmProvider>>();

        var sut = new FallbackLlmProvider(primary.Object, fallback.Object, logger.Object);

        var result = await sut.CompleteAsync("system", "user");

        Assert.Equal("primary response", result);
        fallback.Verify(f => f.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_WhenPrimaryFails_FallsBackToSecondary()
    {
        var primary = new Mock<ILlmProvider>();
        primary.Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("simulated failure"));
        var fallback = new Mock<ILlmProvider>();
        fallback.Setup(f => f.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("fallback response");
        var logger = new Mock<ILogger<FallbackLlmProvider>>();

        var sut = new FallbackLlmProvider(primary.Object, fallback.Object, logger.Object);

        var result = await sut.CompleteAsync("system", "user");

        Assert.Equal("fallback response", result);
    }

    [Fact]
    public async Task EmbedAsync_WhenPrimaryFails_FallsBackToSecondary()
    {
        var primary = new Mock<ILlmProvider>();
        primary.Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new NotSupportedException("no embeddings on this provider"));
        var fallback = new Mock<ILlmProvider>();
        fallback.Setup(f => f.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[] { 0.1f, 0.2f });
        var logger = new Mock<ILogger<FallbackLlmProvider>>();

        var sut = new FallbackLlmProvider(primary.Object, fallback.Object, logger.Object);

        var result = await sut.EmbedAsync("some text");

        Assert.Equal(new float[] { 0.1f, 0.2f }, result);
    }
}
