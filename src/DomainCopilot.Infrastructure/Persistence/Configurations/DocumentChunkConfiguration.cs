using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Content).IsRequired();
        builder.Property(chunk => chunk.ChunkIndex).IsRequired();
        builder.Property(chunk => chunk.ClauseOrSection).HasMaxLength(200);

        builder.Property(chunk => chunk.ExtraMetadata)
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => JsonSerializer.Deserialize<Dictionary<string, string>>(value, (JsonSerializerOptions?)null));

        builder.HasOne(chunk => chunk.Document)
            .WithMany(document => document.Chunks)
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.ChunkIndex }).IsUnique();
    }
}

