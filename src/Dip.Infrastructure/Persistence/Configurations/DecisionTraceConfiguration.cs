// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class DecisionTraceConfiguration : IEntityTypeConfiguration<DecisionTrace>
{
    public void Configure(EntityTypeBuilder<DecisionTrace> builder)
    {
        builder.ToTable("decision_traces");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.DecisionType).HasColumnName("decision_type").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PreviousValue).HasColumnName("previous_value").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NewValue).HasColumnName("new_value").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.EvidenceIdsJson).HasColumnName("evidence_ids_json").HasMaxLength(16000).IsRequired();
        builder.Property(x => x.DetailJson).HasColumnName("detail_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}
