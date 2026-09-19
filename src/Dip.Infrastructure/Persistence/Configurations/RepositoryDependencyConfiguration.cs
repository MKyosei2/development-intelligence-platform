using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for repository dependency edges.
public sealed class RepositoryDependencyConfiguration : IEntityTypeConfiguration<RepositoryDependency>
{
    public void Configure(EntityTypeBuilder<RepositoryDependency> b)
    {
        b.ToTable("repository_dependencies");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.SourceRepositoryId).HasColumnName("source_repository_id");
        b.Property(x => x.TargetRepositoryId).HasColumnName("target_repository_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ContractKey).HasColumnName("contract_key").HasMaxLength(200);
        b.Property(x => x.Critical).HasColumnName("critical");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<RepositoryNode>().WithMany().HasForeignKey(x => x.SourceRepositoryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<RepositoryNode>().WithMany().HasForeignKey(x => x.TargetRepositoryId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.SourceRepositoryId, x.TargetRepositoryId, x.Kind, x.ContractKey }).IsUnique();
    }
}
