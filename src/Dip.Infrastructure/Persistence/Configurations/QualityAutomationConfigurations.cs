// Purpose: Implements Q ua li ty Au to ma ti on Co nf ig ur at io ns persistence behavior.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class QualityAutomationProfileConfiguration : IEntityTypeConfiguration<QualityAutomationProfile>
{
    public void Configure(EntityTypeBuilder<QualityAutomationProfile> b)
    {
        b.ToTable("quality_automation_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        b.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        b.Property(x => x.ChangedPixelWarningPercent).HasColumnName("changed_pixel_warning_percent").HasPrecision(8, 4).IsRequired();
        b.Property(x => x.ChangedPixelFailurePercent).HasColumnName("changed_pixel_failure_percent").HasPrecision(8, 4).IsRequired();
        b.Property(x => x.MinimumSsim).HasColumnName("minimum_ssim").HasPrecision(8, 6).IsRequired();
        b.Property(x => x.AssetRulesJson).HasColumnName("asset_rules_json").HasColumnType("text").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.ProjectId).IsUnique();
    }
}

public sealed class GoldenMasterProfileConfiguration : IEntityTypeConfiguration<GoldenMasterProfile>
{
    public void Configure(EntityTypeBuilder<GoldenMasterProfile> b)
    {
        b.ToTable("golden_master_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        b.Property(x => x.CaptureKey).HasColumnName("capture_key").HasMaxLength(500).IsRequired();
        b.Property(x => x.ArtifactRecordId).HasColumnName("artifact_record_id").IsRequired();
        b.Property(x => x.SceneKey).HasColumnName("scene_key").HasMaxLength(300).IsRequired();
        b.Property(x => x.CameraKey).HasColumnName("camera_key").HasMaxLength(300).IsRequired();
        b.Property(x => x.Width).HasColumnName("width").IsRequired();
        b.Property(x => x.Height).HasColumnName("height").IsRequired();
        b.Property(x => x.MaskJson).HasColumnName("mask_json").HasColumnType("text").IsRequired();
        b.Property(x => x.Active).HasColumnName("active").IsRequired();
        b.Property(x => x.ApprovedBy).HasColumnName("approved_by").HasMaxLength(300).IsRequired();
        b.Property(x => x.ApprovedAtUtc).HasColumnName("approved_at_utc").IsRequired();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ArtifactRecord>().WithMany().HasForeignKey(x => x.ArtifactRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.CaptureKey, x.Active });
    }
}

public sealed class VisualComparisonRecordConfiguration : IEntityTypeConfiguration<VisualComparisonRecord>
{
    public void Configure(EntityTypeBuilder<VisualComparisonRecord> b)
    {
        b.ToTable("visual_comparison_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        b.Property(x => x.GoldenMasterProfileId).HasColumnName("golden_master_profile_id").IsRequired();
        b.Property(x => x.BaselineArtifactRecordId).HasColumnName("baseline_artifact_record_id").IsRequired();
        b.Property(x => x.CurrentArtifactRecordId).HasColumnName("current_artifact_record_id").IsRequired();
        b.Property(x => x.DiffArtifactRecordId).HasColumnName("diff_artifact_record_id");
        b.Property(x => x.Verdict).HasColumnName("verdict").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.ChangedPixelPercent).HasColumnName("changed_pixel_percent").HasPrecision(8, 4).IsRequired();
        b.Property(x => x.Ssim).HasColumnName("ssim").HasPrecision(8, 6).IsRequired();
        b.Property(x => x.ChangedPixelCount).HasColumnName("changed_pixel_count").IsRequired();
        b.Property(x => x.EvaluatedPixelCount).HasColumnName("evaluated_pixel_count").IsRequired();
        b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(2000).IsRequired();
        b.Property(x => x.ComparedAtUtc).HasColumnName("compared_at_utc").IsRequired();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<GoldenMasterProfile>().WithMany().HasForeignKey(x => x.GoldenMasterProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ArtifactRecord>().WithMany().HasForeignKey(x => x.BaselineArtifactRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ArtifactRecord>().WithMany().HasForeignKey(x => x.CurrentArtifactRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ArtifactRecord>().WithMany().HasForeignKey(x => x.DiffArtifactRecordId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.ProjectId, x.ComparedAtUtc });
    }
}

public sealed class AssetQaFindingRecordConfiguration : IEntityTypeConfiguration<AssetQaFindingRecord>
{
    public void Configure(EntityTypeBuilder<AssetQaFindingRecord> b)
    {
        b.ToTable("asset_qa_finding_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        b.Property(x => x.EvidenceId).HasColumnName("evidence_id").IsRequired();
        b.Property(x => x.Source).HasColumnName("source").HasMaxLength(80).IsRequired();
        b.Property(x => x.RuleId).HasColumnName("rule_id").HasMaxLength(160).IsRequired();
        b.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        b.Property(x => x.Message).HasColumnName("message").HasMaxLength(4000).IsRequired();
        b.Property(x => x.RelativePath).HasColumnName("relative_path").HasMaxLength(1000);
        b.Property(x => x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(128).IsRequired();
        b.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("text").IsRequired();
        b.Property(x => x.State).HasColumnName("state").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        b.Property(x => x.ResolvedAtUtc).HasColumnName("resolved_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<EvidenceRecord>().WithMany().HasForeignKey(x => x.EvidenceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.Fingerprint, x.State });
        b.HasIndex(x => new { x.ProjectId, x.ObservedAtUtc });
    }
}

public sealed class ClaimEvidenceAssessmentRecordConfiguration : IEntityTypeConfiguration<ClaimEvidenceAssessmentRecord>
{
    public void Configure(EntityTypeBuilder<ClaimEvidenceAssessmentRecord> b)
    {
        b.ToTable("claim_evidence_assessment_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        b.Property(x => x.ClaimId).HasColumnName("claim_id").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.DiscrepancyScore).HasColumnName("discrepancy_score").HasPrecision(8, 4).IsRequired();
        b.Property(x => x.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
        b.Property(x => x.SupportingEvidenceJson).HasColumnName("supporting_evidence_json").HasColumnType("text").IsRequired();
        b.Property(x => x.ContradictingEvidenceJson).HasColumnName("contradicting_evidence_json").HasColumnType("text").IsRequired();
        b.Property(x => x.ReasonCodesJson).HasColumnName("reason_codes_json").HasColumnType("text").IsRequired();
        b.Property(x => x.AssessedAtUtc).HasColumnName("assessed_at_utc").IsRequired();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ProjectClaim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.AssessedAtUtc });
        b.HasIndex(x => new { x.ClaimId, x.AssessedAtUtc });
    }
}
