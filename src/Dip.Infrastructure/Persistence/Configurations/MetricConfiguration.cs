using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class MetricConfiguration : IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> builder)
    {
        builder.ToTable("metrics");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Value).HasPrecision(28, 8);
        builder.Property(x => x.BaselineValue).HasPrecision(28, 8);
        builder.Property(x => x.Difference).HasPrecision(28, 8);
        builder.Property(x => x.DifferencePercent).HasPrecision(12, 4);
        builder.Property(x => x.EnvironmentId).HasMaxLength(100);
        builder.Property(x => x.ScenarioId).HasMaxLength(100);
        builder.Property(x => x.Platform).HasMaxLength(100);
        builder.Property(x => x.BuildConfiguration).HasMaxLength(100);
        builder.Property(x => x.BenchmarkVersion).HasMaxLength(100);

        builder.HasIndex(x => x.PipelineRunId);
        builder.HasIndex(x => new
        {
            x.PipelineRunId,
            x.Name,
            x.EnvironmentId,
            x.ScenarioId
        });
    }
}
