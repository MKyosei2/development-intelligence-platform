// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class DevelopmentGraphNodeConfiguration : IEntityTypeConfiguration<DevelopmentGraphNode>
{
    public void Configure(EntityTypeBuilder<DevelopmentGraphNode> b)
    {
        b.ToTable("development_graph_nodes"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.ProjectId).HasColumnName("project_id");
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
        b.HasOne<DevelopmentGraphNode>().WithMany().HasForeignKey(x => x.SourceNodeId).OnDelete(DeleteBehavior.Cascade); b.HasOne<DevelopmentGraphNode>().WithMany().HasForeignKey(x => x.TargetNodeId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.SourceNodeId, x.TargetNodeId, x.Type }).IsUnique();
         b.HasIndex(x => new { x.ProjectId, x.SourceNodeId });
         b.HasIndex(x => new { x.ProjectId, x.TargetNodeId });
    }
}
