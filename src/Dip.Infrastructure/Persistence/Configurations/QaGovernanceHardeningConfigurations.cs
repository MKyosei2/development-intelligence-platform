// Purpose: Maps project membership and hardened QA governance processing, override, and backup-verification records to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
{
    public void Configure(EntityTypeBuilder<ProjectMembership> b)
    {
        b.ToTable("project_memberships");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.SubjectId).HasColumnName("subject_id").HasMaxLength(200).IsRequired();
        b.Property(x => x.Login).HasColumnName("login").HasMaxLength(200).IsRequired();
        b.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.GrantedBy).HasColumnName("granted_by").HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.SubjectId }).IsUnique();
        b.HasIndex(x => x.SubjectId);
    }
}

public sealed class QaGovernanceProcessedRunConfiguration : IEntityTypeConfiguration<QaGovernanceProcessedRun>
{
    public void Configure(EntityTypeBuilder<QaGovernanceProcessedRun> b)
    {
        b.ToTable("qa_governance_processed_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.QaRunId).HasColumnName("qa_run_id");
        b.Property(x => x.GovernanceVersion).HasColumnName("governance_version").HasMaxLength(80);
        b.Property(x => x.InputHash).HasColumnName("input_hash").HasMaxLength(64);
        b.Property(x => x.ResultHash).HasColumnName("result_hash").HasMaxLength(64);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.FailureDetail).HasColumnName("failure_detail").HasMaxLength(4000);
        b.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.QaRunId).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class QaReleaseGateOverrideConfiguration : IEntityTypeConfiguration<QaReleaseGateOverride>
{
    public void Configure(EntityTypeBuilder<QaReleaseGateOverride> b)
    {
        b.ToTable("qa_release_gate_overrides");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.PolicyEvaluationId).HasColumnName("policy_evaluation_id");
        b.Property(x => x.QaRunId).HasColumnName("qa_run_id");
        b.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000);
        b.Property(x => x.RequestedBy).HasColumnName("requested_by").HasMaxLength(200);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ApprovedBy).HasColumnName("approved_by").HasMaxLength(200);
        b.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        b.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Status, x.ExpiresAtUtc });
    }
}

public sealed class QaBackupVerificationRecordConfiguration : IEntityTypeConfiguration<QaBackupVerificationRecord>
{
    public void Configure(EntityTypeBuilder<QaBackupVerificationRecord> b)
    {
        b.ToTable("qa_backup_verifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.BackupKind).HasColumnName("backup_kind").HasMaxLength(80);
        b.Property(x => x.BackupReference).HasColumnName("backup_reference").HasMaxLength(1000);
        b.Property(x => x.SourceCommit).HasColumnName("source_commit").HasMaxLength(64);
        b.Property(x => x.EnvironmentFingerprint).HasColumnName("environment_fingerprint").HasMaxLength(128);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Detail).HasColumnName("detail").HasMaxLength(4000);
        b.Property(x => x.VerifiedAtUtc).HasColumnName("verified_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.VerifiedAtUtc });
    }
}
