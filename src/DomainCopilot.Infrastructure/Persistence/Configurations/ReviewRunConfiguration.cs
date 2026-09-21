using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public sealed class ReviewRunConfiguration : IEntityTypeConfiguration<ReviewRun>
{
    public void Configure(EntityTypeBuilder<ReviewRun> builder)
    {
        builder.ToTable("ReviewRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.InitiatedBy).HasMaxLength(200).IsRequired();
        builder.Property(run => run.CorrelationId).HasMaxLength(64);
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(run => run.TerminationReason).HasMaxLength(1_000);
        builder.HasIndex(run => run.DocumentId);
        builder.HasIndex(run => run.StartedAtUtc);
        builder.HasOne<Document>().WithMany().HasForeignKey(run => run.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ReviewMemo>().WithMany().HasForeignKey(run => run.ReviewMemoId).OnDelete(DeleteBehavior.SetNull);
    }
}
