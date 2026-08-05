using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class QualityGateResultConfiguration : IEntityTypeConfiguration<QualityGateResult>
{
    public void Configure(EntityTypeBuilder<QualityGateResult> builder)
    {
        builder.ToTable("quality_gate_results");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Score).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.ReasonsJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.PipelineRunId).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
    }
}
