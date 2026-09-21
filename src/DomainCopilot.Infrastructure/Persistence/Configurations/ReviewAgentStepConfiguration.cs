using DomainCopilot.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public sealed class ReviewAgentStepConfiguration : IEntityTypeConfiguration<ReviewAgentStep>
{
    public void Configure(EntityTypeBuilder<ReviewAgentStep> builder)
    {
        builder.ToTable("ReviewAgentSteps");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.AgentName).HasMaxLength(100).IsRequired();
        builder.Property(step => step.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(step => step.FailureReason).HasMaxLength(1_000);
        builder.HasIndex(step => new { step.ReviewRunId, step.Sequence }).IsUnique();
        builder.HasOne<ReviewRun>().WithMany(run => run.Steps).HasForeignKey(step => step.ReviewRunId).OnDelete(DeleteBehavior.Cascade);
    }
}
