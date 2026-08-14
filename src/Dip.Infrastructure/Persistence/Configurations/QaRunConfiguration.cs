using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class QaRunConfiguration : IEntityTypeConfiguration<QaRun>
{
    public void Configure(EntityTypeBuilder<QaRun> b)
    {
        b.ToTable("qa_runs"); b.HasKey(x=>x.Id);
        b.Property(x=>x.Id).HasColumnName("id");
        b.Property(x=>x.ProjectId).HasColumnName("project_id");
        b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        b.Property(x=>x.Language).HasColumnName("language").HasConversion(v=>v.Value,v=>LanguageId.Parse(v)).HasMaxLength(40).IsRequired();
        b.Property(x=>x.WorkspaceRelativePath).HasColumnName("workspace_relative_path").HasMaxLength(500).IsRequired();
        b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x=>x.ArtifactRoot).HasColumnName("artifact_root").HasMaxLength(1000).IsRequired();
        b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x=>x.FindingCount).HasColumnName("finding_count");
        b.Property(x=>x.CriticalFindingCount).HasColumnName("critical_finding_count");
        b.Property(x=>x.WarningFindingCount).HasColumnName("warning_finding_count");
        b.Property(x=>x.SummaryJson).HasColumnName("summary_json").HasMaxLength(32000);
        b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc");
        b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});
    }
}
