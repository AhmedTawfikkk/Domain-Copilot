using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Documents.Review;

public sealed class StreamingLegalReviewService : IStreamingLegalReviewService
{
    private readonly ILegalReviewOrchestrator _orchestrator;
    private readonly ILogger<StreamingLegalReviewService> _logger;

    public StreamingLegalReviewService(
        ILegalReviewOrchestrator orchestrator,
        ILogger<StreamingLegalReviewService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async IAsyncEnumerable<LegalReviewProgressEvent> StreamAsync(
        LegalReviewRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var channel = Channel.CreateUnbounded<LegalReviewProgressEvent>(
            new UnboundedChannelOptions { SingleReader = true });

        var execution = ExecuteAsync(
            request,
            new ChannelProgressReporter(channel.Writer),
            channel.Writer,
            cancellationToken);

        await foreach (var progressEvent in channel.Reader
                           .ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return progressEvent;
        }

        await execution.ConfigureAwait(false);
    }

    private async Task ExecuteAsync(
        LegalReviewRequest request,
        IReviewProgressReporter progressReporter,
        ChannelWriter<LegalReviewProgressEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _orchestrator.ReviewAsync(
                request,
                cancellationToken,
                progressReporter);

            writer.TryWrite(new LegalReviewProgressEvent(
                result.Status switch
                {
                    LegalReviewStatus.Completed => LegalReviewProgressEventType.Completed,
                    LegalReviewStatus.Cancelled => LegalReviewProgressEventType.Cancelled,
                    _ => LegalReviewProgressEventType.Terminated
                },
                result.ReviewRunId == Guid.Empty ? null : result.ReviewRunId,
                Result: result,
                Message: result.TerminationMessage));

            writer.TryComplete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Legal review stream failed.");

            writer.TryWrite(new LegalReviewProgressEvent(
                LegalReviewProgressEventType.Error,
                null,
                Message: "The legal review could not be completed."));

            writer.TryComplete();
        }
    }

    private sealed class ChannelProgressReporter : IReviewProgressReporter
    {
        private readonly ChannelWriter<LegalReviewProgressEvent> _writer;

        public ChannelProgressReporter(ChannelWriter<LegalReviewProgressEvent> writer) =>
            _writer = writer;

        public ValueTask ReportAsync(
            LegalReviewProgressEvent progressEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _writer.TryWrite(progressEvent);
            return ValueTask.CompletedTask;
        }
    }
}
