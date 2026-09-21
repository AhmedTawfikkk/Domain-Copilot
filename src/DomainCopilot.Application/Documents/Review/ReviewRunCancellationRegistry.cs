using System.Collections.Concurrent;

namespace DomainCopilot.Application.Documents.Review;

public sealed class ReviewRunCancellationRegistry : IReviewRunCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public IReviewRunCancellationRegistration Register(
        Guid reviewRunId,
        CancellationToken requestCancellationToken = default)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(
            requestCancellationToken);

        if (!_sources.TryAdd(reviewRunId, source))
        {
            source.Dispose();
            throw new InvalidOperationException(
                "A cancellation registration already exists for this review run.");
        }

        return new Registration(_sources, reviewRunId, source);
    }

    public bool TryCancel(Guid reviewRunId)
    {
        if (!_sources.TryGetValue(reviewRunId, out var source))
        {
            return false;
        }

        source.Cancel();
        return true;
    }

    private sealed class Registration : IReviewRunCancellationRegistration
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources;
        private readonly Guid _reviewRunId;
        private readonly CancellationTokenSource _source;
        private int _disposed;

        public Registration(
            ConcurrentDictionary<Guid, CancellationTokenSource> sources,
            Guid reviewRunId,
            CancellationTokenSource source)
        {
            _sources = sources;
            _reviewRunId = reviewRunId;
            _source = source;
        }

        public CancellationToken CancellationToken => _source.Token;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _sources.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(
                _reviewRunId,
                _source));

            _source.Dispose();
        }
    }
}
