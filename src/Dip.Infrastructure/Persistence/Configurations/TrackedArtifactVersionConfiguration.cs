// Purpose: Configures persistence mapping for TrackedArtifactVersion.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class TrackedArtifactVersionConfiguration : IEntityTypeConfiguration<TrackedArtifactVersion>
{
    public void Configure(EntityTypeBuilder<TrackedArtifactVersion> builder)
    {
        builder.ToTable("tracked_artifact_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ArtifactId).HasColumnName("artifact_id").IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(x => x.ChangeKind).HasColumnName("change_kind").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SourceAgentId).HasColumnName("source_agent_id").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SourceRootId).HasColumnName("source_root_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayPath).HasColumnName("display_path").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.PreviousDisplayPath).HasColumnName("previous_display_path").HasMaxLength(1000);
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        builder.Property(x => x.FileModifiedAtUtc).HasColumnName("file_modified_at_utc");
        builder.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<TrackedArtifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ArtifactId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => x.SourceEventId).IsUnique();
        builder.HasIndex(x => new { x.ArtifactId, x.ObservedAtUtc });
    }
}
