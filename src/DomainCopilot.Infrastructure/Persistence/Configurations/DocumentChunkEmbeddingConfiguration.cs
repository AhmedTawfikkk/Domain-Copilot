using DomainCopilot.Domain.Entites;
using DomainCopilot.Infrastructure.Persistence.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations
{
    public sealed class DocumentChunkEmbeddingConfiguration
     : IEntityTypeConfiguration<DocumentChunkEmbedding>
    {
        public void Configure(EntityTypeBuilder<DocumentChunkEmbedding> builder)
        {
            builder.ToTable("DocumentChunkEmbeddings");

            builder.HasKey(embedding => embedding.DocumentChunkId);

            builder.Property(embedding => embedding.Embedding)
                .HasColumnType("vector(768)")
                .IsRequired();

            builder.Property(embedding => embedding.CreatedAtUtc)
                .IsRequired();

            builder.Property(embedding => embedding.UpdatedAtUtc)
                .IsRequired();

            builder.HasOne<DocumentChunk>()
                .WithOne()
                .HasForeignKey<DocumentChunkEmbedding>(
                    embedding => embedding.DocumentChunkId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(embedding => embedding.Embedding)
                .HasDatabaseName("IX_DocumentChunkEmbeddings_Embedding_Hnsw")
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasStorageParameter("m", 16)
                .HasStorageParameter("ef_construction", 64);
        }
    }
}
