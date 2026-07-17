using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;
public sealed class MetricConfiguration:IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> b)
    {
        b.ToTable("metrics");b.HasKey(x=>x.Id);
        b.Property(x=>x.Name).HasMaxLength(120).IsRequired();b.Property(x=>x.Unit).HasMaxLength(30).IsRequired();
        b.Property(x=>x.Value).HasPrecision(20,6);b.Property(x=>x.BaselineValue).HasPrecision(20,6);
        b.Property(x=>x.Difference).HasPrecision(20,6);b.Property(x=>x.DifferencePercent).HasPrecision(20,6);
        b.Property(x=>x.EnvironmentId).HasMaxLength(120);b.Property(x=>x.ScenarioId).HasMaxLength(120);
        b.Property(x=>x.Platform).HasMaxLength(80);b.Property(x=>x.BuildConfiguration).HasMaxLength(80);
        b.Property(x=>x.BenchmarkVersion).HasMaxLength(80);
        b.HasIndex(x=>new{x.PipelineRunId,x.Name,x.EnvironmentId,x.ScenarioId}).IsUnique();
    }
}
