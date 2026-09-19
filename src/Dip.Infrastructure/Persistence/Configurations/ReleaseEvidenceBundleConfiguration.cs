using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for release evidence bundles.
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
