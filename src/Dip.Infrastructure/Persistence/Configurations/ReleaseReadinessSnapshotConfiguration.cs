using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for release readiness snapshots.
public sealed class ReleaseReadinessSnapshotConfiguration : IEntityTypeConfiguration<ReleaseReadinessSnapshot>
{
    public void Configure(EntityTypeBuilder<ReleaseReadinessSnapshot> b)
    {
        b.ToTable("release_readiness_snapshots");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x => x.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Confidence).HasColumnName("confidence").HasPrecision(5, 2);
        b.Property(x => x.BlockersJson).HasColumnName("blockers_json").HasMaxLength(16000);
        b.Property(x => x.WarningsJson).HasColumnName("warnings_json").HasMaxLength(16000);
        b.Property(x => x.RequiredActionsJson).HasColumnName("required_actions_json").HasMaxLength(16000);
        b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000);
        b.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
    }
}
