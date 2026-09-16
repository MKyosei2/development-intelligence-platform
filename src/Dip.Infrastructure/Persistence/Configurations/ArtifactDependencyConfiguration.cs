// Purpose: Configures persistence mapping for ArtifactDependency.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ArtifactDependencyConfiguration : IEntityTypeConfiguration<ArtifactDependency>
{
    public void Configure(EntityTypeBuilder<ArtifactDependency> builder)
    {
        builder.ToTable("artifact_dependencies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ArtifactId).HasColumnName("artifact_id").IsRequired();
        builder.Property(x => x.TargetKey).HasColumnName("target_key").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.HasOne<TrackedArtifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ArtifactId, x.Kind, x.TargetKey });
    }
}
