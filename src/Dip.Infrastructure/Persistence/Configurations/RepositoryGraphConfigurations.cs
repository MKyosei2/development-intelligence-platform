// Purpose: Maps repository topology, dependencies, cross-repository impact, and development graph relationships to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

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

public sealed class DevelopmentGraphNodeConfiguration : IEntityTypeConfiguration<DevelopmentGraphNode>
{
    public void Configure(EntityTypeBuilder<DevelopmentGraphNode> b)
    {
        b.ToTable("development_graph_nodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Type).HasColumnName("node_type").HasConversion<string>().HasMaxLength(60);
        b.Property(x => x.Key).HasColumnName("node_key").HasMaxLength(300);
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(300);
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(80);
        b.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(80);
        b.Property(x => x.ExternalReference).HasColumnName("external_reference").HasMaxLength(2000);
        b.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(64000);
        b.Property(x => x.FirstObservedAtUtc).HasColumnName("first_observed_at_utc");
        b.Property(x => x.LastObservedAtUtc).HasColumnName("last_observed_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Type, x.Key }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Type, x.Status });
        b.HasIndex(x => new { x.ProjectId, x.LastObservedAtUtc });
    }
}

public sealed class DevelopmentGraphEdgeConfiguration : IEntityTypeConfiguration<DevelopmentGraphEdge>
{
    public void Configure(EntityTypeBuilder<DevelopmentGraphEdge> b)
    {
        b.ToTable("development_graph_edges");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.SourceNodeId).HasColumnName("source_node_id");
        b.Property(x => x.TargetNodeId).HasColumnName("target_node_id");
        b.Property(x => x.Type).HasColumnName("edge_type").HasConversion<string>().HasMaxLength(60);
        b.Property(x => x.Weight).HasColumnName("weight").HasPrecision(6, 2);
        b.Property(x => x.Critical).HasColumnName("critical");
        b.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(32000);
        b.Property(x => x.FirstObservedAtUtc).HasColumnName("first_observed_at_utc");
        b.Property(x => x.LastObservedAtUtc).HasColumnName("last_observed_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<DevelopmentGraphNode>().WithMany().HasForeignKey(x => x.SourceNodeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<DevelopmentGraphNode>().WithMany().HasForeignKey(x => x.TargetNodeId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.SourceNodeId, x.TargetNodeId, x.Type }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.SourceNodeId });
        b.HasIndex(x => new { x.ProjectId, x.TargetNodeId });
    }
}
