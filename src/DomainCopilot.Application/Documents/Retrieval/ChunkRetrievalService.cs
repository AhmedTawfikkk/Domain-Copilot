using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Application.Documents.Retrieval
{
    public sealed class ChunkRetrievalService : IChunkRetrievalService
    {
        private const int ReciprocalRankFusionConstant = 60;

        private readonly IChunkRetrievalRepository _repository;
        private readonly ILlmProvider _llmProvider;

        public ChunkRetrievalService(
            IChunkRetrievalRepository repository,
            ILlmProvider llmProvider)
        {
            _repository = repository;
            _llmProvider = llmProvider;
        }

        public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            string query,
            RetreivalMode mode,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);

            if (limit is < 1 or > 20)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    "Limit must be between 1 and 20.");
            }

            return mode switch
            {
                RetreivalMode.Dense => await SearchDenseAsync(
                    query,
                    limit,
                    cancellationToken),

                RetreivalMode.Keyword => await _repository.SearchKeywordAsync(
                    query,
                    limit,
                    cancellationToken),

                RetreivalMode.Hybrid => await SearchHybridAsync(
                    query,
                    limit,
                    cancellationToken),

                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }

        private async Task<IReadOnlyList<RetrievedChunk>> SearchDenseAsync(
            string query,
            int limit,
            CancellationToken cancellationToken)
        {
            var embedding = await _llmProvider.EmbedAsync(
                query,
                cancellationToken);

            return await _repository.SearchDenseAsync(
                embedding,
                limit,
                cancellationToken);
        }

        private async Task<IReadOnlyList<RetrievedChunk>> SearchHybridAsync(
            string query,
            int limit,
            CancellationToken cancellationToken)
        {
            var candidateCount = Math.Max(limit * 3, 20);

            var embedding = await _llmProvider.EmbedAsync(
                query,
                cancellationToken);

            var denseTask = _repository.SearchDenseAsync(
                embedding,
                candidateCount,
                cancellationToken);

            var keywordTask = _repository.SearchKeywordAsync(
                query,
                candidateCount,
                cancellationToken);

            await Task.WhenAll(denseTask, keywordTask);

            return FuseByReciprocalRank(
                denseTask.Result,
                keywordTask.Result,
                limit);
        }

        private static IReadOnlyList<RetrievedChunk> FuseByReciprocalRank(
            IReadOnlyList<RetrievedChunk> denseResults,
            IReadOnlyList<RetrievedChunk> keywordResults,
            int limit)
        {
            var fused = new Dictionary<Guid, (RetrievedChunk Chunk, double Score)>();

            AddResults(denseResults, fused);
            AddResults(keywordResults, fused);

            return fused.Values
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Chunk.DocumentChunkId)
                .Take(limit)
                .Select(result => result.Chunk with
                {
                    Score = result.Score
                })
                .ToList();
        }

        private static void AddResults(
            IReadOnlyList<RetrievedChunk> results,
            IDictionary<Guid, (RetrievedChunk Chunk, double Score)> fused)
        {
            for (var index = 0; index < results.Count; index++)
            {
                var chunk = results[index];
                var rrfScore = 1d / (ReciprocalRankFusionConstant + index + 1);

                if (fused.TryGetValue(
                    chunk.DocumentChunkId,
                    out var existing))
                {
                    fused[chunk.DocumentChunkId] =
                        (existing.Chunk, existing.Score + rrfScore);

                    continue;
                }

                fused[chunk.DocumentChunkId] = (chunk, rrfScore);
            }
        }
    }
}