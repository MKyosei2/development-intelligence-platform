// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class MetricConfiguration : IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> builder)
    {
        // Keep the runtime model aligned with MetricCoreConfiguration and the
        // immutable legacy migration baseline.
        builder.ToTable("Metrics");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Value).HasPrecision(18, 6);
        builder.Property(x => x.BaselineValue).HasPrecision(18, 6);
        builder.Property(x => x.Difference).HasPrecision(18, 6);
        builder.Property(x => x.DifferencePercent).HasPrecision(12, 4);
        builder.Property(x => x.EnvironmentId).HasMaxLength(120);
        builder.Property(x => x.ScenarioId).HasMaxLength(120);
        builder.Property(x => x.Platform).HasMaxLength(80);
        builder.Property(x => x.BuildConfiguration).HasMaxLength(80);
        builder.Property(x => x.BenchmarkVersion).HasMaxLength(80);

        builder.HasOne<PipelineRun>()
            .WithMany()
            .HasForeignKey(x => x.PipelineRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PipelineRunId, x.Name });
    }
}
