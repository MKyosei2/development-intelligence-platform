// Purpose: Configures EF Core persistence for P ro je ct Tr ut hS na ps ho tC on fi gu ra ti on.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProjectTruthSnapshotConfiguration : IEntityTypeConfiguration<ProjectTruthSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectTruthSnapshot> builder)
    {
        builder.ToTable("project_truth_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.TruthStatus).HasColumnName("truth_status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.RiskLevel).HasColumnName("risk_level").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.HealthScore).HasColumnName("health_score").HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.VerifiedProgress).HasColumnName("verified_progress").HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.ReportedProgress).HasColumnName("reported_progress").HasPrecision(5, 2);
        builder.Property(x => x.EvidenceConfidence).HasColumnName("evidence_confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.BlockerCount).HasColumnName("blocker_count").IsRequired();
        builder.Property(x => x.ContradictionCount).HasColumnName("contradiction_count").IsRequired();
        builder.Property(x => x.UnverifiedClaimCount).HasColumnName("unverified_claim_count").IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.BlockersJson).HasColumnName("blockers_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.TopActionsJson).HasColumnName("top_actions_json").HasMaxLength(16000).IsRequired();
        builder.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc").IsRequired();

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
    }
}
