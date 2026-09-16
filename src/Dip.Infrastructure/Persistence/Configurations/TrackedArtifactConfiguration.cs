// Purpose: Configures persistence mapping for TrackedArtifact.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class TrackedArtifactConfiguration : IEntityTypeConfiguration<TrackedArtifact>
{
    public void Configure(EntityTypeBuilder<TrackedArtifact> builder)
    {
        builder.ToTable("tracked_artifacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.PathKey).HasColumnName("path_key").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DisplayPath).HasColumnName("display_path").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Extension).HasColumnName("extension").HasMaxLength(40).IsRequired();
        builder.Property(x => x.LifecycleState).HasColumnName("lifecycle_state").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.CurrentVersionNumber).HasColumnName("current_version_number").IsRequired();
        builder.Property(x => x.CurrentSha256).HasColumnName("current_sha256").HasMaxLength(64);
        builder.Property(x => x.CurrentSizeBytes).HasColumnName("current_size_bytes");
        builder.Property(x => x.CurrentFileModifiedAtUtc).HasColumnName("current_file_modified_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.PathKey }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.UpdatedAtUtc });
    }
}
