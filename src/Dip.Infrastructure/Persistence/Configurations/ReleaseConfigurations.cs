// Purpose: Maps release evidence bundles and release-readiness snapshots to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ReleaseEvidenceBundleConfiguration : IEntityTypeConfiguration<ReleaseEvidenceBundle>
{
    public void Configure(EntityTypeBuilder<ReleaseEvidenceBundle> b)
    {
        b.ToTable("release_evidence_bundles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x => x.BuildId).HasColumnName("build_id").HasMaxLength(200);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ManifestArtifactId).HasColumnName("manifest_artifact_id").HasMaxLength(64);
        b.Property(x => x.SbomArtifactId).HasColumnName("sbom_artifact_id").HasMaxLength(64);
        b.Property(x => x.BundleArtifactId).HasColumnName("bundle_artifact_id").HasMaxLength(64);
        b.Property(x => x.ArtifactSha256).HasColumnName("artifact_sha256").HasMaxLength(64);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}

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
