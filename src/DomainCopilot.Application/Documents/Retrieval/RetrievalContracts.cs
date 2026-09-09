
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Application.Documents.Retrieval
{
    public enum RetreivalMode
    {
        Dense,
        Keyword,
        Hybrid
    }
    public sealed record ChunkEmbeddingWorkItem(
    Guid DocumentChunkId,
    string Content);
    public sealed record ChunkEmbeddingWrite(
    Guid DocumentChunkId,
    float[] Embedding);
    public sealed record EmbeddingIndexingResult(
        int IndexedChunkCount);
    public sealed record RetrievedChunk(
    Guid DocumentChunkId,
    Guid DocumentId,
    string FileName,
    string Source,
    string Version,
    string Content,
    string? ClauseOrSection,
    int? PageNumber,
    bool LowConfidence,
    double Score);

    public interface IChunkRetrievalRepository
    {
        Task<IReadOnlyList<ChunkEmbeddingWorkItem>> GetChunksWithoutEmbeddingsAsync(
            int take,
            CancellationToken cancellationToken = default);

        Task UpsertEmbeddingsAsync(
            IReadOnlyCollection<ChunkEmbeddingWrite> embeddings,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RetrievedChunk>> SearchDenseAsync(
            float[] queryEmbedding,
            int take,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RetrievedChunk>> SearchKeywordAsync(
            string query,
            int take,
            CancellationToken cancellationToken = default);
    }

    public interface IEmbeddingIndexingService
    {
        Task<EmbeddingIndexingResult> IndexPendingChunksAsync(
            int batchSize,
            CancellationToken cancellationToken = default);
    }

    public interface IChunkRetrievalService
    {
        Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            string query,
            RetreivalMode mode,
            int limit,
            CancellationToken cancellationToken = default);
    }

}

