using DomainCopilot.Infrastructure.Persistence.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Configurations;

public sealed class LlmRequestTelemetryConfiguration
    : IEntityTypeConfiguration<LlmRequestTelemetry>
{
    public void Configure(EntityTypeBuilder<LlmRequestTelemetry> builder)
    {
        builder.ToTable("LlmRequestTelemetry");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.CorrelationId)
            .HasMaxLength(64);

        builder.Property(entry => entry.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.Model)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(entry => entry.Operation)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.EstimatedCostUsd)
            .HasPrecision(18, 8);

        builder.Property(entry => entry.FailureReason)
            .HasMaxLength(1_000);

        builder.Property(entry => entry.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(entry => entry.CreatedAtUtc);

        builder.HasIndex(entry => entry.CorrelationId);

        builder.HasIndex(entry => new
        {
            entry.Provider,
            entry.Operation,
            entry.CreatedAtUtc
        });
    }
}
