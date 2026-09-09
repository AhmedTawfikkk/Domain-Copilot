using DomainCopilot.Application.Documents.Retrieval;
using DomainCopilot.Infrastructure.Persistence.Entites;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Persistence.Repositories
{
    public sealed class ChunkRetrievalRepository : IChunkRetrievalRepository
    {
        private readonly DomainCopilotDbContext _dbContext;

        public ChunkRetrievalRepository(
            DomainCopilotDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<ChunkEmbeddingWorkItem>>
     GetChunksWithoutEmbeddingsAsync(
         int take,
         CancellationToken cancellationToken = default)
        {
            var chunks = await _dbContext.DocumentChunks
                .AsNoTracking()
                .Where(chunk => !_dbContext.DocumentChunkEmbeddings
                    .Any(embedding =>
                        embedding.DocumentChunkId == chunk.Id))
                .OrderBy(chunk => chunk.DocumentId)
                .ThenBy(chunk => chunk.ChunkIndex)
                .Take(take)
                .Select(chunk => new ChunkEmbeddingWorkItem(
                    chunk.Id,
                    chunk.Content))
                .ToListAsync(cancellationToken);

            return chunks;
        }

        public async Task UpsertEmbeddingsAsync(
            IReadOnlyCollection<ChunkEmbeddingWrite> embeddings,
            CancellationToken cancellationToken = default)
        {
            if (embeddings.Count == 0)
            {
                return;
            }

            var ids = embeddings
                .Select(embedding => embedding.DocumentChunkId)
                .Distinct()
                .ToList();

            var existingEmbeddings = await _dbContext.DocumentChunkEmbeddings
                .Where(embedding =>
                    ids.Contains(embedding.DocumentChunkId))
                .ToDictionaryAsync(
                    embedding => embedding.DocumentChunkId,
                    cancellationToken);

            var now = DateTime.UtcNow;

            foreach (var write in embeddings)
            {
                if (existingEmbeddings.TryGetValue(
                    write.DocumentChunkId,
                    out var existing))
                {
                    existing.Embedding = new Vector(write.Embedding);
                    existing.UpdatedAtUtc = now;
                    continue;
                }

                _dbContext.DocumentChunkEmbeddings.Add(
                    new DocumentChunkEmbedding
                    {
                        DocumentChunkId = write.DocumentChunkId,
                        Embedding = new Vector(write.Embedding),
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<RetrievedChunk>> SearchDenseAsync(
            float[] queryEmbedding,
            int take,
            CancellationToken cancellationToken = default)
        {
            var queryVector = new Vector(queryEmbedding);

            var results = await (
                from embedding in _dbContext.DocumentChunkEmbeddings.AsNoTracking()
                join chunk in _dbContext.DocumentChunks.AsNoTracking()
                    on embedding.DocumentChunkId equals chunk.Id
                join document in _dbContext.Documents.AsNoTracking()
                    on chunk.DocumentId equals document.Id
                orderby embedding.Embedding.CosineDistance(queryVector)
                select new RetrievedChunk(
                    chunk.Id,
                    document.Id,
                    document.FileName,
                    document.Source,
                    document.Version,
                    chunk.Content,
                    chunk.ClauseOrSection,
                    chunk.PageNumber,
                    chunk.LowConfidence,
                    1d - embedding.Embedding.CosineDistance(queryVector))
            )
            .Take(take)
            .ToListAsync(cancellationToken);

            return results;
        }

        public async Task<IReadOnlyList<RetrievedChunk>> SearchKeywordAsync(
        string query,
        int take,
        CancellationToken cancellationToken = default)
        {
            var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
            var wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
            SELECT c."Id", c."DocumentId", d."FileName", d."Source", d."Version",
                   c."Content", c."ClauseOrSection", c."PageNumber", c."LowConfidence",
                   ts_rank_cd(c."SearchVector", websearch_to_tsquery('english', @query)) AS "Rank"
            FROM "DocumentChunks" c
            JOIN "Documents" d ON d."Id" = c."DocumentId"
            WHERE c."SearchVector" @@ websearch_to_tsquery('english', @query)
            ORDER BY "Rank" DESC, c."Id"
            LIMIT @take;
            """;

                command.Parameters.Add(new NpgsqlParameter("query", query));
                command.Parameters.Add(new NpgsqlParameter("take", take));

                var results = new List<RetrievedChunk>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new RetrievedChunk(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.IsDBNull(6) ? null : reader.GetString(6),
                        reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        reader.GetBoolean(8),
                        reader.GetDouble(9)));
                }

                return results;
            }
            finally
            {
                if (wasClosed) await connection.CloseAsync();
            }
        }
    }
    }
