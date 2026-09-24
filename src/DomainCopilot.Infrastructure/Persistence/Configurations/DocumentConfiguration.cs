using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(document => document.Id);

        builder.Property(document => document.FileName).IsRequired().HasMaxLength(500);
        builder.Property(document => document.Source).IsRequired().HasMaxLength(500);
        builder.Property(document => document.FileHash).IsRequired().HasMaxLength(128);
        builder.Property(document => document.Version).IsRequired().HasMaxLength(50);

        builder.Property(document => document.SourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(document => document.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // The content hash is only unique per owner. Two different users may
        // legitimately upload the same file, and each must own an isolated copy.
        builder.HasIndex(document => new { document.FileHash, document.OwnerId }).IsUnique();

        builder.HasMany(document => document.Chunks)
            .WithOne(chunk => chunk.Document)
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
