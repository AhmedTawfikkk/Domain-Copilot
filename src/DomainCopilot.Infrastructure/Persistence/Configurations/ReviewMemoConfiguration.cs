using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public sealed class ReviewMemoConfiguration
    : IEntityTypeConfiguration<ReviewMemo>
{
    public void Configure(EntityTypeBuilder<ReviewMemo> builder)
    {
        builder.ToTable("ReviewMemos");

        builder.HasKey(memo => memo.Id);

        builder.Property(memo => memo.DocumentId)
            .IsRequired();

        builder.Property(memo => memo.OriginalContent)
            .IsRequired();

        builder.Property(memo => memo.Content)
            .IsRequired();

        builder.Property(memo => memo.ApprovalStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(memo => memo.CreatedAtUtc)
            .IsRequired();

        builder.Property(memo => memo.DecidedBy)
            .HasMaxLength(200);

        builder.Property(memo => memo.DecisionComment)
            .HasMaxLength(2_000);

        builder.HasIndex(memo => memo.DocumentId);

        builder.HasIndex(memo => memo.ApprovalStatus);

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(memo => memo.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}