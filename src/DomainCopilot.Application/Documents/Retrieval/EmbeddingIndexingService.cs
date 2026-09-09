using DomainCopilot.Application.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Application.Documents.Retrieval
{
    public sealed class EmbeddingIndexingService : IEmbeddingIndexingService
    {
        private const int EmbeddingDimensions = 768;

        private readonly IChunkRetrievalRepository _repository;
        private readonly ILlmProvider _llmProvider;

        public EmbeddingIndexingService(
            IChunkRetrievalRepository repository,
            ILlmProvider llmProvider)
        {
            _repository = repository;
            _llmProvider = llmProvider;
        }

        public async Task<EmbeddingIndexingResult> IndexPendingChunksAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            if (batchSize is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchSize),
                    "Batch size must be between 1 and 100.");
            }

            var indexedChunkCount = 0;

            while (true)
            {
                var pendingChunks = await _repository
                    .GetChunksWithoutEmbeddingsAsync(
                        batchSize,
                        cancellationToken);

                if (pendingChunks.Count == 0)
                {
                    break;
                }

                var writes = new List<ChunkEmbeddingWrite>(pendingChunks.Count);

                foreach (var chunk in pendingChunks)
                {
                    var embedding = await _llmProvider.EmbedAsync(
                        chunk.Content,
                        cancellationToken);

                    if (embedding.Length != EmbeddingDimensions)
                    {
                        throw new InvalidOperationException(
                            $"Expected {EmbeddingDimensions} embedding dimensions, " +
                            $"but received {embedding.Length}.");
                    }

                    writes.Add(new ChunkEmbeddingWrite(
                        chunk.DocumentChunkId,
                        embedding));
                }

                await _repository.UpsertEmbeddingsAsync(
                    writes,
                    cancellationToken);

                indexedChunkCount += writes.Count;

                if (pendingChunks.Count < batchSize)
                {
                    break;
                }
            }

            return new EmbeddingIndexingResult(indexedChunkCount);
        }
    }
}
