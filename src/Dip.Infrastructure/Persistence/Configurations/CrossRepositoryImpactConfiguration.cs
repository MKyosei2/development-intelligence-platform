using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for cross-repository impact analysis.
public sealed class CrossRepositoryImpactConfiguration : IEntityTypeConfiguration<CrossRepositoryImpact>
{
    public void Configure(EntityTypeBuilder<CrossRepositoryImpact> b)
    {
        b.ToTable("cross_repository_impacts");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x => x.SourceRepositoryId).HasColumnName("source_repository_id");
        b.Property(x => x.Level).HasColumnName("level").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.AffectedRepositoryIdsJson).HasColumnName("affected_repository_ids_json").HasMaxLength(16000);
        b.Property(x => x.ReasonsJson).HasColumnName("reasons_json").HasMaxLength(16000);
        b.Property(x => x.AnalyzedAtUtc).HasColumnName("analyzed_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<RepositoryNode>().WithMany().HasForeignKey(x => x.SourceRepositoryId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.CommitSha, x.AnalyzedAtUtc });
    }
}
