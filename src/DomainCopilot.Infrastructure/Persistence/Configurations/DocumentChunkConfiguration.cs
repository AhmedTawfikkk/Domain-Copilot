using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using System.Text.Json;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Content)
            .IsRequired();

        builder.Property(chunk => chunk.ChunkIndex)
            .IsRequired();

        builder.Property(chunk => chunk.ExtractionConfidence)
    .HasDefaultValue(1.0)
    .IsRequired();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DocumentChunks_ExtractionConfidence",
            "\"ExtractionConfidence\" >= 0.0 AND \"ExtractionConfidence\" <= 1.0"));

        builder.Property(chunk => chunk.ClauseOrSection)
            .HasMaxLength(200);

        builder.Property(chunk => chunk.ExtraMetadata)
            .HasConversion(
                value => JsonSerializer.Serialize(
                    value,
                    (JsonSerializerOptions?)null),
                value => JsonSerializer.Deserialize<
                    Dictionary<string, string>>(
                    value,
                    (JsonSerializerOptions?)null));

        // PostgreSQL maintains this generated full-text-search column.
        // It is not part of the Domain entity.
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasComputedColumnSql(
                "to_tsvector('english', coalesce(\"Content\", ''))",
                stored: true);

        builder.HasIndex("SearchVector")
            .HasDatabaseName("IX_DocumentChunks_SearchVector_Gin")
            .HasMethod("GIN");

        builder.HasOne(chunk => chunk.Document)
            .WithMany(document => document.Chunks)
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(chunk => new
        {
            chunk.DocumentId,
            chunk.ChunkIndex
        }).IsUnique();
    }
}

