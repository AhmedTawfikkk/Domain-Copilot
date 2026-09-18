using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public sealed class ReviewMemoRiskFindingConfiguration
    : IEntityTypeConfiguration<ReviewMemoRiskFinding>
{
    public void Configure(
        EntityTypeBuilder<ReviewMemoRiskFinding> builder)
    {
        builder.ToTable("ReviewMemoRiskFindings");

        builder.HasKey(finding => finding.Id);

        builder.Property(finding => finding.RuleId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(finding => finding.ClauseType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(finding => finding.Severity)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(finding => finding.Title)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(finding => finding.Rationale)
            .IsRequired();

        builder.Property(finding => finding.Recommendation)
            .IsRequired();

        builder.Property(finding => finding.DisplayOrder)
            .IsRequired();

        builder.HasIndex(finding => new
        {
            finding.ReviewMemoId,
            finding.DisplayOrder
        }).IsUnique();

        builder.HasOne(finding => finding.ReviewMemo)
            .WithMany(memo => memo.RiskFindings)
            .HasForeignKey(finding => finding.ReviewMemoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}