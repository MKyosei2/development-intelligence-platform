using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class QualityIssueConfiguration : IEntityTypeConfiguration<QualityIssue>
{
    public void Configure(EntityTypeBuilder<QualityIssue> builder)
    {
        builder.ToTable("QualityIssues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(1000);
        builder.Property(x => x.RuleId).HasMaxLength(200);
        builder.Property(x => x.EvidenceJson).HasMaxLength(16000);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.Fingerprint });
        builder.HasIndex(x => new { x.ProjectId, x.LastSeenAtUtc });
    }
}
