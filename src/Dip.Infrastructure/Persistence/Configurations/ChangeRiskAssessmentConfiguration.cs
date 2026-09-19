using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for change risk assessments.
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
