using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for repository graph nodes.
public sealed class RepositoryNodeConfiguration : IEntityTypeConfiguration<RepositoryNode>
{
    public void Configure(EntityTypeBuilder<RepositoryNode> b)
    {
        b.ToTable("repository_nodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Key).HasColumnName("key").HasMaxLength(160);
        b.Property(x => x.RepositoryUrl).HasColumnName("repository_url").HasMaxLength(1000);
        b.Property(x => x.DefaultBranch).HasColumnName("default_branch").HasMaxLength(200);
        b.Property(x => x.Role).HasColumnName("role").HasMaxLength(120);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
    }
}
