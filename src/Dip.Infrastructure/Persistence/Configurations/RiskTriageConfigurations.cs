// Purpose: Maps change-risk assessments and regression triage results to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ChangeRiskAssessmentConfiguration : IEntityTypeConfiguration<ChangeRiskAssessment>
{
    public void Configure(EntityTypeBuilder<ChangeRiskAssessment> b)
    {
        b.ToTable("change_risk_assessments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x => x.RiskScore).HasColumnName("risk_score").HasPrecision(5, 2);
        b.Property(x => x.RiskLevel).HasColumnName("risk_level").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ReasonsJson).HasColumnName("reasons_json").HasMaxLength(16000);
        b.Property(x => x.AffectedRepositoriesJson).HasColumnName("affected_repositories_json").HasMaxLength(16000);
        b.Property(x => x.AssessedAtUtc).HasColumnName("assessed_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.CommitSha, x.AssessedAtUtc });
    }
}

public sealed class RegressionTriageConfiguration : IEntityTypeConfiguration<RegressionTriage>
{
    public void Configure(EntityTypeBuilder<RegressionTriage> b)
    {
        b.ToTable("regression_triages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.IncidentId).HasColumnName("incident_id");
        b.Property(x => x.SuspectedCommit).HasColumnName("suspected_commit").HasMaxLength(64);
        b.Property(x => x.SuspectedSubsystem).HasColumnName("suspected_subsystem").HasMaxLength(200);
        b.Property(x => x.SuspectedFilesJson).HasColumnName("suspected_files_json").HasMaxLength(16000);
        b.Property(x => x.SuggestedOwner).HasColumnName("suggested_owner").HasMaxLength(200);
        b.Property(x => x.Confidence).HasColumnName("confidence").HasPrecision(5, 2);
        b.Property(x => x.EvidenceIdsJson).HasColumnName("evidence_ids_json").HasMaxLength(16000);
        b.Property(x => x.Explanation).HasColumnName("explanation").HasMaxLength(4000);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.IncidentId, x.CreatedAtUtc });
    }
}
