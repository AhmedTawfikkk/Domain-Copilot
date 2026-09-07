using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(document => document.Id);

        builder.Property(document => document.FileName).IsRequired();

        builder.Property(document => document.Source).IsRequired();

        builder.Property(document => document.SourceType).IsRequired();

        builder.Property(document => document.FileHash).IsRequired();

        builder.Property(document => document.Status).IsRequired();

        builder.HasIndex(document => document.FileHash)
            .IsUnique();
    }
}
