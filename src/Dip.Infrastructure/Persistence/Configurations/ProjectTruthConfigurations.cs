// Purpose: Maps Project Truth evidence, claims, decisions, and evaluation snapshots to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class EvidenceRecordConfiguration : IEntityTypeConfiguration<EvidenceRecord>
{
    public void Configure(EntityTypeBuilder<EvidenceRecord> builder)
    {
        builder.ToTable("evidence_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.PipelineRunId).HasColumnName("pipeline_run_id");
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.EvidenceKind).HasColumnName("evidence_kind").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.SourceReference).HasColumnName("source_reference").HasMaxLength(1000);
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasMaxLength(16000);
        builder.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.SourceInstanceId).HasColumnName("source_instance_id").HasMaxLength(200);
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(300);
        builder.Property(x => x.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(80).HasDefaultValue(Dip.Domain.Enums.EvidenceEventType.Unknown).IsRequired();
        builder.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(300);
        builder.Property(x => x.MachineId).HasColumnName("machine_id").HasMaxLength(300);
        builder.Property(x => x.ArtifactId).HasColumnName("artifact_id").HasMaxLength(300);
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(128);
        builder.Property(x => x.ParentEvidenceId).HasColumnName("parent_evidence_id");
        builder.Property(x => x.PrivacyClass).HasColumnName("privacy_class").HasConversion<string>().HasMaxLength(40).HasDefaultValue(Dip.Domain.Enums.EvidencePrivacyClass.Internal).IsRequired();
        builder.Property(x => x.IntegrityStatus).HasColumnName("integrity_status").HasConversion<string>().HasMaxLength(40).HasDefaultValue(Dip.Domain.Enums.EvidenceIntegrityStatus.LegacyUnsigned).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(32000);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.ProjectId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.EvidenceKind });
        builder.HasIndex(x => new { x.ProjectId, x.EventType, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.ProjectId, x.SourceType, x.SourceInstanceId });
        builder.HasIndex(x => new { x.ProjectId, x.SourceType, x.SourceInstanceId, x.SourceEventId })
            .IsUnique()
            .HasFilter("\"source_event_id\" IS NOT NULL AND \"source_instance_id\" IS NOT NULL");
    }
}

public sealed class ProjectClaimConfiguration : IEntityTypeConfiguration<ProjectClaim>
{
    public void Configure(EntityTypeBuilder<ProjectClaim> builder)
    {
        builder.ToTable("project_claims");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClaimedStatus).HasColumnName("claimed_status").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ClaimedProgress).HasColumnName("claimed_progress").HasPrecision(5, 2);
        builder.Property(x => x.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(x => x.ClaimedAtUtc).HasColumnName("claimed_at_utc").IsRequired();
        builder.Property(x => x.VerificationStatus).HasColumnName("verification_status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.LastVerifiedAtUtc).HasColumnName("last_verified_at_utc");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.ClaimedAtUtc });
    }
}

public sealed class ClaimEvidenceLinkConfiguration : IEntityTypeConfiguration<ClaimEvidenceLink>
{
    public void Configure(EntityTypeBuilder<ClaimEvidenceLink> builder)
    {
        builder.ToTable("claim_evidence_links");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ClaimId).HasColumnName("claim_id").IsRequired();
        builder.Property(x => x.EvidenceRecordId).HasColumnName("evidence_record_id").IsRequired();
        builder.Property(x => x.Relation).HasColumnName("relation").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<ProjectClaim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EvidenceRecord>().WithMany().HasForeignKey(x => x.EvidenceRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ClaimId, x.EvidenceRecordId }).IsUnique();
    }
}

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
