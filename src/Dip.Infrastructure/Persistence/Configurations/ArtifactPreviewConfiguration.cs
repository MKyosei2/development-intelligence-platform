// Purpose: Configures persistence mapping for ArtifactPreview.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ArtifactPreviewConfiguration : IEntityTypeConfiguration<ArtifactPreview>
{
    public void Configure(EntityTypeBuilder<ArtifactPreview> builder)
    {
        builder.ToTable("artifact_previews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ArtifactVersionId).HasColumnName("artifact_version_id").IsRequired();
        builder.Property(x => x.ArtifactRecordId).HasColumnName("artifact_record_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<TrackedArtifactVersion>().WithMany().HasForeignKey(x => x.ArtifactVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ArtifactRecord>().WithMany().HasForeignKey(x => x.ArtifactRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ArtifactVersionId, x.Kind });
    }
}
