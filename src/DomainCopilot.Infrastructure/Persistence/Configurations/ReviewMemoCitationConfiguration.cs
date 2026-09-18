using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public sealed class ReviewMemoCitationConfiguration
    : IEntityTypeConfiguration<ReviewMemoCitation>
{
    public void Configure(
        EntityTypeBuilder<ReviewMemoCitation> builder)
    {
        builder.ToTable("ReviewMemoCitations");

        builder.HasKey(citation => new
        {
            citation.ReviewMemoId,
            citation.DocumentChunkId
        });

        builder.Property(citation => citation.CitationOrder)
            .IsRequired();

        builder.HasIndex(citation => new
        {
            citation.ReviewMemoId,
            citation.CitationOrder
        }).IsUnique();

        builder.HasOne(citation => citation.ReviewMemo)
            .WithMany(memo => memo.Citations)
            .HasForeignKey(citation => citation.ReviewMemoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(citation => citation.DocumentChunk)
            .WithMany()
            .HasForeignKey(citation => citation.DocumentChunkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}