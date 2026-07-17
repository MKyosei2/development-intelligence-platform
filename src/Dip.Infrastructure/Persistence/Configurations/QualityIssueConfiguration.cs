using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class QualityIssueConfiguration : IEntityTypeConfiguration<QualityIssue>
{
    public void Configure(EntityTypeBuilder<QualityIssue> builder)
    {
        builder.ToTable("quality_issues");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Fingerprint).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(1000);
        builder.Property(x => x.RuleId).HasMaxLength(200);
        builder.Property(x => x.EvidenceJson).HasColumnType("jsonb");

        builder.HasIndex(x => new { x.ProjectId, x.Fingerprint, x.Status });
        builder.HasIndex(x => x.PipelineRunId);
    }
}
