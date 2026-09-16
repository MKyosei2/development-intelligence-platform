// Purpose: Configures EF Core mapping and indexes for adaptive QA plan snapshots.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class AdaptiveQaPlanSnapshotConfiguration : IEntityTypeConfiguration<AdaptiveQaPlanSnapshot>
{
    public void Configure(EntityTypeBuilder<AdaptiveQaPlanSnapshot> builder)
    {
        builder.ToTable("adaptive_qa_plan_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.ChangeSetHash).HasColumnName("change_set_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RiskScore).HasColumnName("risk_score").HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.EstimatedMinutes).HasColumnName("estimated_minutes").HasPrecision(8, 2).IsRequired();
        builder.Property(x => x.PlanJson).HasColumnName("plan_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}
