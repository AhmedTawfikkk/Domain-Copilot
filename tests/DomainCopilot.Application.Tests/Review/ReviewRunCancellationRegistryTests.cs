using DomainCopilot.Application.Documents.Review;

namespace DomainCopilot.Application.Tests.Review;

public sealed class ReviewRunCancellationRegistryTests
{
    [Fact]
    public void TryCancel_WhenRunIsRegistered_CancelsOnlyThatRun()
    {
        var registry = new ReviewRunCancellationRegistry();
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();

        using var firstRegistration = registry.Register(firstRunId);
        using var secondRegistration = registry.Register(secondRunId);

        var wasCancelled = registry.TryCancel(firstRunId);

        Assert.True(wasCancelled);
        Assert.True(firstRegistration.CancellationToken.IsCancellationRequested);
        Assert.False(secondRegistration.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void TryCancel_WhenRunIsNotActive_ReturnsFalse()
    {
        var registry = new ReviewRunCancellationRegistry();

        Assert.False(registry.TryCancel(Guid.NewGuid()));
    }

    [Fact]
    public void Dispose_RemovesRunFromRegistry()
    {
        var registry = new ReviewRunCancellationRegistry();
        var runId = Guid.NewGuid();

        var registration = registry.Register(runId);
        registration.Dispose();

        Assert.False(registry.TryCancel(runId));
    }
}
