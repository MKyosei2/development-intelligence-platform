using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class QualityGateResultConfiguration : IEntityTypeConfiguration<QualityGateResult>
{
    public void Configure(EntityTypeBuilder<QualityGateResult> builder)
    {
        builder.ToTable("QualityGateResults");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Score).HasPrecision(5, 2);
        builder.Property(x => x.ReasonsJson).HasMaxLength(16000).IsRequired();
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
    }
}
