using Dip.Infrastructure.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dip.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DipDbContext))]
public sealed class DipDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.4");

        // Frozen from AdaptiveQaPlanSnapshotConfiguration.cs: AdaptiveQaPlanSnapshotConfiguration
        modelBuilder.Entity<AdaptiveQaPlanSnapshot>(b =>
        {
            b.ToTable("adaptive_qa_plan_snapshots");
                    b.HasKey(x=>x.Id);
                    b.Property(x=>x.Id).HasColumnName("id");
                    b.Property(x=>x.ProjectId).HasColumnName("project_id").IsRequired();
                    b.Property(x=>x.ChangeSetHash).HasColumnName("change_set_hash").HasMaxLength(64).IsRequired();
                    b.Property(x=>x.RiskScore).HasColumnName("risk_score").HasPrecision(5,2).IsRequired();
                    b.Property(x=>x.EstimatedMinutes).HasColumnName("estimated_minutes").HasPrecision(8,2).IsRequired();
                    b.Property(x=>x.PlanJson).HasColumnName("plan_json").HasMaxLength(32000).IsRequired();
                    b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                    b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});
        });

        // Frozen from AuditLogConfiguration.cs: AuditLogConfiguration
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_logs");
                    b.HasKey(x => x.Id);
                    b.Property(x => x.Id).HasColumnName("id");
                    b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
                    b.Property(x => x.Category).HasColumnName("category").HasMaxLength(80).IsRequired();
                    b.Property(x => x.Action).HasColumnName("action").HasMaxLength(120).IsRequired();
                    b.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(200).IsRequired();
                    b.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200);
                    b.Property(x => x.DetailJson).HasColumnName("detail_json").HasMaxLength(16000);
                    b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            
                    b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
        });

        // Frozen from ClaimEvidenceLinkConfiguration.cs: ClaimEvidenceLinkConfiguration
        modelBuilder.Entity<ClaimEvidenceLink>(b =>
        {
            b.ToTable("claim_evidence_links");
                    b.HasKey(x => x.Id);
                    b.Property(x => x.Id).HasColumnName("id");
                    b.Property(x => x.ClaimId).HasColumnName("claim_id").IsRequired();
                    b.Property(x => x.EvidenceRecordId).HasColumnName("evidence_record_id").IsRequired();
                    b.Property(x => x.Relation).HasColumnName("relation").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
                    b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            
                    b.HasOne<ProjectClaim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
                    b.HasOne<EvidenceRecord>().WithMany().HasForeignKey(x => x.EvidenceRecordId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.ClaimId, x.EvidenceRecordId }).IsUnique();
        });

        // Frozen from DecisionTraceConfiguration.cs: DecisionTraceConfiguration
        modelBuilder.Entity<DecisionTrace>(b =>
        {
            b.ToTable("decision_traces");
                    b.HasKey(x=>x.Id);
                    b.Property(x=>x.Id).HasColumnName("id");
                    b.Property(x=>x.ProjectId).HasColumnName("project_id").IsRequired();
                    b.Property(x=>x.DecisionType).HasColumnName("decision_type").HasMaxLength(120).IsRequired();
                    b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
                    b.Property(x=>x.PreviousValue).HasColumnName("previous_value").HasMaxLength(500).IsRequired();
                    b.Property(x=>x.NewValue).HasColumnName("new_value").HasMaxLength(500).IsRequired();
                    b.Property(x=>x.Reason).HasColumnName("reason").HasMaxLength(4000).IsRequired();
                    b.Property(x=>x.EvidenceIdsJson).HasColumnName("evidence_ids_json").HasMaxLength(16000).IsRequired();
                    b.Property(x=>x.DetailJson).HasColumnName("detail_json").HasMaxLength(32000).IsRequired();
                    b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                    b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});
        });

        // Frozen from EvidenceRecordConfiguration.cs: EvidenceRecordConfiguration
        modelBuilder.Entity<EvidenceRecord>(b =>
        {
            b.ToTable("evidence_records");
                    b.HasKey(x => x.Id);
                    b.Property(x => x.Id).HasColumnName("id");
                    b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
                    b.Property(x => x.PipelineRunId).HasColumnName("pipeline_run_id");
                    b.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
                    b.Property(x => x.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.EvidenceKind).HasColumnName("evidence_kind").HasMaxLength(120).IsRequired();
                    b.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
                    b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
                    b.Property(x => x.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.SourceReference).HasColumnName("source_reference").HasMaxLength(1000);
                    b.Property(x => x.PayloadJson).HasColumnName("payload_json").HasMaxLength(16000);
                    b.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
                    b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            
                    b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.SetNull);
                    b.HasIndex(x => new { x.ProjectId, x.ObservedAtUtc });
                    b.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.EvidenceKind });
        });

        // Frozen from HardwareCalibrationConfiguration.cs: HardwareCalibrationConfiguration
        modelBuilder.Entity<HardwareCalibration>(b =>
        {
            b.ToTable("hardware_calibrations");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.MachineFingerprint).HasColumnName("machine_fingerprint").HasMaxLength(64).IsRequired();b.Property(x=>x.CpuSingleScore).HasColumnName("cpu_single_score").HasPrecision(12,3);b.Property(x=>x.CpuMultiScore).HasColumnName("cpu_multi_score").HasPrecision(12,3);b.Property(x=>x.GpuScore).HasColumnName("gpu_score").HasPrecision(12,3);b.Property(x=>x.MemoryScore).HasColumnName("memory_score").HasPrecision(12,3);b.Property(x=>x.StorageScore).HasColumnName("storage_score").HasPrecision(12,3);b.Property(x=>x.AvailableMemoryMb).HasColumnName("available_memory_mb");b.Property(x=>x.EnvironmentJson).HasColumnName("environment_json").HasMaxLength(32000).IsRequired();b.Property(x=>x.MeasuredAtUtc).HasColumnName("measured_at_utc");b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>new{x.ProjectId,x.MeasuredAtUtc});
        });

        // Frozen from HardwareCommunitySampleConfiguration.cs: HardwareCommunitySampleConfiguration
        modelBuilder.Entity<HardwareCommunitySample>(b =>
        {
            b.ToTable("hardware_community_samples");
                    b.HasKey(x=>x.Id);
                    b.Property(x=>x.Id).HasColumnName("id");
                    b.Property(x=>x.ProfileKey).HasColumnName("profile_key").HasMaxLength(160).IsRequired();
                    b.Property(x=>x.Platform).HasColumnName("platform").HasMaxLength(80).IsRequired();
                    b.Property(x=>x.HardwareKeyHash).HasColumnName("hardware_key_hash").HasMaxLength(64).IsRequired();
                    b.Property(x=>x.OsFamily).HasColumnName("os_family").HasMaxLength(120).IsRequired();
                    b.Property(x=>x.DriverFamily).HasColumnName("driver_family").HasMaxLength(160).IsRequired();
                    b.Property(x=>x.CpuSingleScore).HasColumnName("cpu_single_score").HasPrecision(12,3).IsRequired();
                    b.Property(x=>x.CpuMultiScore).HasColumnName("cpu_multi_score").HasPrecision(12,3).IsRequired();
                    b.Property(x=>x.GpuScore).HasColumnName("gpu_score").HasPrecision(12,3).IsRequired();
                    b.Property(x=>x.MemoryScore).HasColumnName("memory_score").HasPrecision(12,3).IsRequired();
                    b.Property(x=>x.StorageScore).HasColumnName("storage_score").HasPrecision(12,3).IsRequired();
                    b.Property(x=>x.ThermalRetentionPercent).HasColumnName("thermal_retention_percent").HasPrecision(5,2).IsRequired();
                    b.Property(x=>x.AvailableMemoryMb).HasColumnName("available_memory_mb").IsRequired();
                    b.Property(x=>x.RealDevice).HasColumnName("real_device").IsRequired();
                    b.Property(x=>x.ConsentGranted).HasColumnName("consent_granted").IsRequired();
                    b.Property(x=>x.MeasuredAtUtc).HasColumnName("measured_at_utc").IsRequired();
                    b.Property(x=>x.ReceivedAtUtc).HasColumnName("received_at_utc").IsRequired();
                    b.HasIndex(x=>new{x.ProfileKey,x.Platform,x.ReceivedAtUtc});
                    b.HasIndex(x=>new{x.HardwareKeyHash,x.MeasuredAtUtc});
        });

        // Frozen from HardwareProfileConfiguration.cs: HardwareProfileConfiguration
        modelBuilder.Entity<HardwareProfile>(b =>
        {
            b.ToTable("hardware_profiles");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();b.Property(x=>x.Platform).HasColumnName("platform").HasMaxLength(80).IsRequired();b.Property(x=>x.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.CpuSingleScore).HasColumnName("cpu_single_score").HasPrecision(12,3);b.Property(x=>x.CpuMultiScore).HasColumnName("cpu_multi_score").HasPrecision(12,3);b.Property(x=>x.GpuScore).HasColumnName("gpu_score").HasPrecision(12,3);b.Property(x=>x.MemoryScore).HasColumnName("memory_score").HasPrecision(12,3);b.Property(x=>x.StorageScore).HasColumnName("storage_score").HasPrecision(12,3);b.Property(x=>x.ThermalScore).HasColumnName("thermal_score").HasPrecision(12,3);b.Property(x=>x.MemoryBudgetMb).HasColumnName("memory_budget_mb");b.Property(x=>x.TargetFps).HasColumnName("target_fps");b.Property(x=>x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(32000).IsRequired();b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.HasIndex(x=>new{x.Name,x.Platform}).IsUnique();
        });

        // Frozen from LegacyCoreConfigurations.cs: ProjectCoreConfiguration
        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("Projects"); b.HasKey(x => x.Id);
                    b.Property(x => x.Name).HasMaxLength(120).IsRequired();
                    b.Property(x => x.RepositoryUrl).HasMaxLength(2000).IsRequired();
                    b.Property(x => x.DefaultBranch).HasMaxLength(200).IsRequired();
                    b.Property(x => x.HealthScore).HasConversion(v => v.Value, v => new HealthScore(v)).HasPrecision(5, 2).IsRequired();
                    b.HasIndex(x => x.RepositoryUrl);
        });

        // Frozen from LegacyCoreConfigurations.cs: PipelineRunCoreConfiguration
        modelBuilder.Entity<PipelineRun>(b =>
        {
            b.ToTable("PipelineRuns"); b.HasKey(x => x.Id);
                    b.Property(x => x.CommitSha).HasConversion(v => v.Value, v => new CommitSha(v)).HasMaxLength(64).IsRequired();
                    b.Property(x => x.ExternalRunId).HasMaxLength(200).IsRequired();
                    b.Property(x => x.Branch).HasMaxLength(200).IsRequired();
                    b.Property(x => x.CollectorType).HasMaxLength(100).IsRequired();
                    b.Property(x => x.CollectorVersion).HasMaxLength(50).IsRequired();
                    b.Property(x => x.FailureReason).HasMaxLength(2000);
                    b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.ProjectId, x.ExternalRunId, x.CollectorType, x.AttemptNumber }).IsUnique();
                    b.HasIndex(x => new { x.ProjectId, x.QueuedAtUtc });
        });

        // Frozen from LegacyCoreConfigurations.cs: MetricCoreConfiguration
        modelBuilder.Entity<Metric>(b =>
        {
            b.ToTable("Metrics"); b.HasKey(x => x.Id);
                    b.Property(x => x.Name).HasMaxLength(120).IsRequired();
                    b.Property(x => x.Unit).HasMaxLength(30).IsRequired();
                    b.Property(x => x.Value).HasPrecision(18, 6);
                    b.Property(x => x.BaselineValue).HasPrecision(18, 6);
                    b.Property(x => x.Difference).HasPrecision(18, 6);
                    b.Property(x => x.DifferencePercent).HasPrecision(12, 4);
                    b.Property(x => x.EnvironmentId).HasMaxLength(120);
                    b.Property(x => x.ScenarioId).HasMaxLength(120);
                    b.Property(x => x.Platform).HasMaxLength(80);
                    b.Property(x => x.BuildConfiguration).HasMaxLength(80);
                    b.Property(x => x.BenchmarkVersion).HasMaxLength(80);
                    b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.PipelineRunId, x.Name });
        });

        // Frozen from LegacyCoreConfigurations.cs: QualityIssueCoreConfiguration
        modelBuilder.Entity<QualityIssue>(b =>
        {
            b.ToTable("QualityIssues"); b.HasKey(x => x.Id);
                    b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
                    b.Property(x => x.Category).HasMaxLength(80).IsRequired();
                    b.Property(x => x.Title).HasMaxLength(200).IsRequired();
                    b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
                    b.Property(x => x.Source).HasMaxLength(1000);
                    b.Property(x => x.RuleId).HasMaxLength(200);
                    b.Property(x => x.EvidenceJson).HasMaxLength(16000);
                    b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.ProjectId, x.Fingerprint });
                    b.HasIndex(x => new { x.ProjectId, x.LastSeenAtUtc });
        });

        // Frozen from LegacyCoreConfigurations.cs: QualityGateResultCoreConfiguration
        modelBuilder.Entity<QualityGateResult>(b =>
        {
            b.ToTable("QualityGateResults"); b.HasKey(x => x.Id);
                    b.Property(x => x.Status).HasMaxLength(40).IsRequired();
                    b.Property(x => x.Score).HasPrecision(5, 2);
                    b.Property(x => x.ReasonsJson).HasMaxLength(16000).IsRequired();
                    b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
        });

        // Frozen from LegacyCoreConfigurations.cs: OutboxMessageCoreConfiguration
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages"); b.HasKey(x => x.Id);
                    b.Property(x => x.MessageType).HasMaxLength(500).IsRequired();
                    b.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
                    b.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
                    b.Property(x => x.CausationId).HasMaxLength(200);
                    b.Property(x => x.LastError).HasMaxLength(4000);
                    b.HasIndex(x => new { x.ProcessedAtUtc, x.NextAttemptAtUtc, x.LockedUntilUtc });
        });

        // Frozen from LegacyCoreConfigurations.cs: ProcessedWebhookCoreConfiguration
        modelBuilder.Entity<ProcessedWebhook>(b =>
        {
            b.ToTable("ProcessedWebhooks"); b.HasKey(x => x.DeliveryId);
                    b.Property(x => x.DeliveryId).HasMaxLength(200);
                    b.Property(x => x.EventName).HasMaxLength(100).IsRequired();
                    b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
                    b.Property(x => x.LastError).HasMaxLength(4000);
                    b.HasIndex(x => x.ReceivedAtUtc);
        });

        // Frozen from P2IntelligenceRecordConfiguration.cs: P2IntelligenceRecordConfiguration
        modelBuilder.Entity<P2IntelligenceRecord>(b =>
        {
            b.ToTable("p2_intelligence_records");
                    b.HasKey(x=>x.Id);
                    b.Property(x=>x.Id).HasColumnName("id");
                    b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
                    b.Property(x=>x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(80).IsRequired();
                    b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x=>x.Score).HasColumnName("score").HasPrecision(5,2);
                    b.Property(x=>x.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x=>x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
                    b.Property(x=>x.PayloadJson).HasColumnName("payload_json").HasMaxLength(32000).IsRequired();
                    b.Property(x=>x.SourceReference).HasColumnName("source_reference").HasMaxLength(1000);
                    b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                    b.Property(x=>x.ProjectId).HasColumnName("project_id").IsRequired();
                    b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x=>new{x.ProjectId,x.Kind,x.CreatedAtUtc});
                    b.HasIndex(x=>new{x.ProjectId,x.SubjectKey,x.CreatedAtUtc});
        });

        // Frozen from PerformanceSimulationRunConfiguration.cs: PerformanceSimulationRunConfiguration
        modelBuilder.Entity<PerformanceSimulationRun>(b =>
        {
            b.ToTable("performance_simulation_runs");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.HardwareProfileId).HasColumnName("hardware_profile_id");b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();b.Property(x=>x.ArtifactDirectory).HasColumnName("artifact_directory").HasMaxLength(1000).IsRequired();b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.ConfidencePercent).HasColumnName("confidence_percent").HasPrecision(5,2);b.Property(x=>x.ResultJson).HasColumnName("result_json").HasMaxLength(32000);b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc");b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc");b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasOne<HardwareProfile>().WithMany().HasForeignKey(x=>x.HardwareProfileId).OnDelete(DeleteBehavior.Restrict);b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: ArtifactBlobConfiguration
        modelBuilder.Entity<ArtifactBlob>(b =>
        {
            b.ToTable("artifact_blobs");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();b.Property(x=>x.SizeBytes).HasColumnName("size_bytes");b.Property(x=>x.ContentType).HasColumnName("content_type").HasMaxLength(200).IsRequired();b.Property(x=>x.StorageProvider).HasColumnName("storage_provider").HasMaxLength(40).IsRequired();b.Property(x=>x.StorageKey).HasColumnName("storage_key").HasMaxLength(1000).IsRequired();b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.HasIndex(x=>new{x.StorageProvider,x.Sha256}).IsUnique();b.HasIndex(x=>new{x.StorageProvider,x.StorageKey}).IsUnique();
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: ArtifactRecordConfiguration
        modelBuilder.Entity<ArtifactRecord>(b =>
        {
            b.ToTable("artifact_records");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.ArtifactBlobId).HasColumnName("artifact_blob_id");b.Property(x=>x.Kind).HasColumnName("kind").HasMaxLength(120).IsRequired();b.Property(x=>x.FileName).HasColumnName("file_name").HasMaxLength(500).IsRequired();b.Property(x=>x.RetentionClass).HasColumnName("retention_class").HasMaxLength(40).IsRequired();b.Property(x=>x.JobId).HasColumnName("job_id");b.Property(x=>x.QaRunId).HasColumnName("qa_run_id");b.Property(x=>x.IncidentId).HasColumnName("incident_id");b.Property(x=>x.BenchmarkRunId).HasColumnName("benchmark_run_id");b.Property(x=>x.GameReplayRunId).HasColumnName("game_replay_run_id");b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.Property(x=>x.ExpiresAtUtc).HasColumnName("expires_at_utc");b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasOne<ArtifactBlob>().WithMany().HasForeignKey(x=>x.ArtifactBlobId).OnDelete(DeleteBehavior.Restrict);b.HasOne<DurableJob>().WithMany().HasForeignKey(x=>x.JobId).OnDelete(DeleteBehavior.SetNull);b.HasOne<QaRun>().WithMany().HasForeignKey(x=>x.QaRunId).OnDelete(DeleteBehavior.SetNull);b.HasOne<Incident>().WithMany().HasForeignKey(x=>x.IncidentId).OnDelete(DeleteBehavior.SetNull);b.HasOne<BenchmarkRun>().WithMany().HasForeignKey(x=>x.BenchmarkRunId).OnDelete(DeleteBehavior.SetNull);b.HasOne<GameReplayRun>().WithMany().HasForeignKey(x=>x.GameReplayRunId).OnDelete(DeleteBehavior.SetNull);b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});b.HasIndex(x=>x.JobId);b.HasIndex(x=>x.IncidentId);b.HasIndex(x=>x.BenchmarkRunId);b.HasIndex(x=>x.GameReplayRunId);
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: DurableJobConfiguration
        modelBuilder.Entity<DurableJob>(b =>
        {
            b.ToTable("durable_jobs");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(80);b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200);b.Property(x=>x.PayloadJson).HasColumnName("payload_json").HasColumnType("text");b.Property(x=>x.ResultJson).HasColumnName("result_json").HasColumnType("text");b.Property(x=>x.Attempt).HasColumnName("attempt");b.Property(x=>x.MaxAttempts).HasColumnName("max_attempts");b.Property(x=>x.ProgressPercent).HasColumnName("progress_percent").HasPrecision(5,2);b.Property(x=>x.CurrentStage).HasColumnName("current_stage").HasMaxLength(200);b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.Property(x=>x.QueuedAtUtc).HasColumnName("queued_at_utc");b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc");b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc");b.Property(x=>x.HeartbeatAtUtc).HasColumnName("heartbeat_at_utc");b.Property(x=>x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(200);b.Property(x=>x.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");b.Property(x=>x.CancellationRequestedAtUtc).HasColumnName("cancellation_requested_at_utc");b.Property(x=>x.FailureCode).HasColumnName("failure_code").HasMaxLength(120);b.Property(x=>x.FailureMessage).HasColumnName("failure_message").HasMaxLength(8000);b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>new{x.Status,x.QueuedAtUtc});b.HasIndex(x=>x.LeaseExpiresAtUtc);b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: IncidentConfiguration
        modelBuilder.Entity<Incident>(b =>
        {
            b.ToTable("incidents");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(128);b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200);b.Property(x=>x.Title).HasColumnName("title").HasMaxLength(240);b.Property(x=>x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.FirstSeenAtUtc).HasColumnName("first_seen_at_utc");b.Property(x=>x.LastSeenAtUtc).HasColumnName("last_seen_at_utc");b.Property(x=>x.OccurrenceCount).HasColumnName("occurrence_count");b.Property(x=>x.ReproducedCount).HasColumnName("reproduced_count");b.Property(x=>x.SuspectedRegressionCommit).HasColumnName("suspected_regression_commit").HasMaxLength(64);b.Property(x=>x.FixedCommit).HasColumnName("fixed_commit").HasMaxLength(64);b.Property(x=>x.RootCauseSummary).HasColumnName("root_cause_summary").HasMaxLength(4000);b.Property(x=>x.ResolvedAtUtc).HasColumnName("resolved_at_utc");b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>new{x.ProjectId,x.Fingerprint}).IsUnique();b.HasIndex(x=>new{x.ProjectId,x.LastSeenAtUtc});b.HasIndex(x=>new{x.ProjectId,x.SubjectKey,x.Status});
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: IncidentOccurrenceConfiguration
        modelBuilder.Entity<IncidentOccurrence>(b =>
        {
            b.ToTable("incident_occurrences");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.IncidentId).HasColumnName("incident_id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200);b.Property(x=>x.QaFindingId).HasColumnName("qa_finding_id");b.Property(x=>x.QaRunId).HasColumnName("qa_run_id");b.Property(x=>x.JobId).HasColumnName("job_id");b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);b.Property(x=>x.DetailJson).HasColumnName("detail_json").HasMaxLength(16000);b.Property(x=>x.ObservedAtUtc).HasColumnName("observed_at_utc");b.HasOne<Incident>().WithMany().HasForeignKey(x=>x.IncidentId).OnDelete(DeleteBehavior.Cascade);b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasOne<QaFinding>().WithMany().HasForeignKey(x=>x.QaFindingId).OnDelete(DeleteBehavior.SetNull);b.HasOne<QaRun>().WithMany().HasForeignKey(x=>x.QaRunId).OnDelete(DeleteBehavior.SetNull);b.HasOne<DurableJob>().WithMany().HasForeignKey(x=>x.JobId).OnDelete(DeleteBehavior.SetNull);b.HasIndex(x=>new{x.IncidentId,x.ObservedAtUtc});b.HasIndex(x=>x.QaFindingId);
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: IncidentEventConfiguration
        modelBuilder.Entity<IncidentEvent>(b =>
        {
            b.ToTable("incident_events");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.IncidentId).HasColumnName("incident_id");b.Property(x=>x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(80);b.Property(x=>x.Summary).HasColumnName("summary").HasMaxLength(2000);b.Property(x=>x.PayloadJson).HasColumnName("payload_json").HasMaxLength(32000);b.Property(x=>x.OccurredAtUtc).HasColumnName("occurred_at_utc");b.HasOne<Incident>().WithMany().HasForeignKey(x=>x.IncidentId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>new{x.IncidentId,x.OccurredAtUtc});
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: BenchmarkDefinitionConfiguration
        modelBuilder.Entity<BenchmarkDefinition>(b =>
        {
            b.ToTable("benchmark_definitions");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.Key).HasColumnName("key").HasMaxLength(160);b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(200);b.Property(x=>x.Kind).HasColumnName("kind").HasMaxLength(80);b.Property(x=>x.ConfigurationJson).HasColumnName("configuration_json").HasMaxLength(32000);b.Property(x=>x.Enabled).HasColumnName("enabled");b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.HasIndex(x=>x.Key).IsUnique();
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: BenchmarkRunConfiguration
        modelBuilder.Entity<BenchmarkRun>(b =>
        {
            b.ToTable("benchmark_runs");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.BenchmarkDefinitionId).HasColumnName("benchmark_definition_id");b.Property(x=>x.JobId).HasColumnName("job_id");b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200);b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);b.Property(x=>x.EnvironmentFingerprint).HasColumnName("environment_fingerprint").HasMaxLength(128);b.Property(x=>x.ConfigurationHash).HasColumnName("configuration_hash").HasMaxLength(64);b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc");b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc");b.Property(x=>x.Summary).HasColumnName("summary").HasMaxLength(4000);b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasOne<BenchmarkDefinition>().WithMany().HasForeignKey(x=>x.BenchmarkDefinitionId).OnDelete(DeleteBehavior.Restrict);b.HasOne<DurableJob>().WithMany().HasForeignKey(x=>x.JobId).OnDelete(DeleteBehavior.Restrict);b.HasIndex(x=>new{x.ProjectId,x.BenchmarkDefinitionId,x.EnvironmentFingerprint,x.ConfigurationHash,x.CreatedAtUtc});b.HasIndex(x=>x.JobId).IsUnique();
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: BenchmarkMetricConfiguration
        modelBuilder.Entity<BenchmarkMetric>(b =>
        {
            b.ToTable("benchmark_metrics");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.BenchmarkRunId).HasColumnName("benchmark_run_id");b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(160);b.Property(x=>x.Value).HasColumnName("value").HasPrecision(20,6);b.Property(x=>x.Unit).HasColumnName("unit").HasMaxLength(40);b.Property(x=>x.Aggregation).HasColumnName("aggregation").HasMaxLength(40);b.HasOne<BenchmarkRun>().WithMany().HasForeignKey(x=>x.BenchmarkRunId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>new{x.BenchmarkRunId,x.Name,x.Aggregation});
        });

        // Frozen from PlatformIntelligenceConfigurations.cs: BenchmarkComparisonConfiguration
        modelBuilder.Entity<BenchmarkComparison>(b =>
        {
            b.ToTable("benchmark_comparisons");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.CurrentRunId).HasColumnName("current_run_id");b.Property(x=>x.BaselineRunId).HasColumnName("baseline_run_id");b.Property(x=>x.MetricName).HasColumnName("metric_name").HasMaxLength(160);b.Property(x=>x.BaselineValue).HasColumnName("baseline_value").HasPrecision(20,6);b.Property(x=>x.CurrentValue).HasColumnName("current_value").HasPrecision(20,6);b.Property(x=>x.DifferencePercent).HasColumnName("difference_percent").HasPrecision(12,3);b.Property(x=>x.Verdict).HasColumnName("verdict").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.Explanation).HasColumnName("explanation").HasMaxLength(2000);b.HasOne<BenchmarkRun>().WithMany().HasForeignKey(x=>x.CurrentRunId).OnDelete(DeleteBehavior.Cascade);b.HasOne<BenchmarkRun>().WithMany().HasForeignKey(x=>x.BaselineRunId).OnDelete(DeleteBehavior.Restrict);b.HasIndex(x=>new{x.CurrentRunId,x.MetricName});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: GameReplaySessionConfiguration
        modelBuilder.Entity<GameReplaySession>(b =>
        {
            b.ToTable("game_replay_sessions"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.Key).HasColumnName("key").HasMaxLength(120); b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(200); b.Property(x=>x.Scene).HasColumnName("scene").HasMaxLength(300); b.Property(x=>x.InputArtifactId).HasColumnName("input_artifact_id").HasMaxLength(1000); b.Property(x=>x.RandomSeed).HasColumnName("random_seed"); b.Property(x=>x.DurationSeconds).HasColumnName("duration_seconds"); b.Property(x=>x.LaunchCommand).HasColumnName("launch_command").HasMaxLength(1000); b.Property(x=>x.Arguments).HasColumnName("arguments").HasMaxLength(4000); b.Property(x=>x.WorkingDirectory).HasColumnName("working_directory").HasMaxLength(1000); b.Property(x=>x.BaselineCommit).HasColumnName("baseline_commit").HasMaxLength(64); b.Property(x=>x.Enabled).HasColumnName("enabled"); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.Key}).IsUnique();
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: GameReplayRunConfiguration
        modelBuilder.Entity<GameReplayRun>(b =>
        {
            b.ToTable("game_replay_runs"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.SessionId).HasColumnName("session_id"); b.Property(x=>x.JobId).HasColumnName("job_id"); b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64); b.Property(x=>x.BuildId).HasColumnName("build_id").HasMaxLength(200); b.Property(x=>x.HardwareFingerprint).HasColumnName("hardware_fingerprint").HasMaxLength(256); b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.SummaryJson).HasColumnName("summary_json").HasMaxLength(32000); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc"); b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasOne<GameReplaySession>().WithMany().HasForeignKey(x=>x.SessionId).OnDelete(DeleteBehavior.Cascade); b.HasOne<DurableJob>().WithMany().HasForeignKey(x=>x.JobId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x=>x.JobId).IsUnique(); b.HasIndex(x=>new{x.ProjectId,x.SessionId,x.HardwareFingerprint,x.FinishedAtUtc});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: GameReplayMetricConfiguration
        modelBuilder.Entity<GameReplayMetric>(b =>
        {
            b.ToTable("game_replay_metrics"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ReplayRunId).HasColumnName("replay_run_id"); b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(160); b.Property(x=>x.Value).HasColumnName("value").HasPrecision(20,6); b.Property(x=>x.Unit).HasColumnName("unit").HasMaxLength(40); b.Property(x=>x.Aggregation).HasColumnName("aggregation").HasMaxLength(40); b.HasOne<GameReplayRun>().WithMany().HasForeignKey(x=>x.ReplayRunId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ReplayRunId,x.Name,x.Aggregation});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: PerformanceBudgetProfileConfiguration
        modelBuilder.Entity<PerformanceBudgetProfile>(b =>
        {
            b.ToTable("performance_budget_profiles"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.Key).HasColumnName("key").HasMaxLength(120); b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(200); b.Property(x=>x.Platform).HasColumnName("platform").HasMaxLength(80); b.Property(x=>x.TargetProfile).HasColumnName("target_profile").HasMaxLength(120); b.Property(x=>x.RulesJson).HasColumnName("rules_json").HasMaxLength(32000); b.Property(x=>x.Enabled).HasColumnName("enabled"); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.Key}).IsUnique(); b.HasIndex(x=>x.Key).IsUnique().HasFilter("project_id IS NULL");
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: PerformanceBudgetEvaluationConfiguration
        modelBuilder.Entity<PerformanceBudgetEvaluation>(b =>
        {
            b.ToTable("performance_budget_evaluations"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.ProfileId).HasColumnName("profile_id"); b.Property(x=>x.ReplayRunId).HasColumnName("replay_run_id"); b.Property(x=>x.BenchmarkRunId).HasColumnName("benchmark_run_id"); b.Property(x=>x.Verdict).HasColumnName("verdict").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.Summary).HasColumnName("summary").HasMaxLength(2000); b.Property(x=>x.DetailJson).HasColumnName("detail_json").HasMaxLength(32000); b.Property(x=>x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasOne<PerformanceBudgetProfile>().WithMany().HasForeignKey(x=>x.ProfileId).OnDelete(DeleteBehavior.Restrict); b.HasOne<GameReplayRun>().WithMany().HasForeignKey(x=>x.ReplayRunId).OnDelete(DeleteBehavior.SetNull); b.HasOne<BenchmarkRun>().WithMany().HasForeignKey(x=>x.BenchmarkRunId).OnDelete(DeleteBehavior.SetNull); b.HasIndex(x=>new{x.ProjectId,x.EvaluatedAtUtc});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: HardwareBacktestConfiguration
        modelBuilder.Entity<HardwareBacktest>(b =>
        {
            b.ToTable("hardware_backtests"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.HardwareProfileId).HasColumnName("hardware_profile_id"); b.Property(x=>x.ScenarioKey).HasColumnName("scenario_key").HasMaxLength(200); b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64); b.Property(x=>x.PredictedFps).HasColumnName("predicted_fps").HasPrecision(12,3); b.Property(x=>x.ActualFps).HasColumnName("actual_fps").HasPrecision(12,3); b.Property(x=>x.PredictionErrorPercent).HasColumnName("prediction_error_percent").HasPrecision(12,3); b.Property(x=>x.EnvironmentFingerprint).HasColumnName("environment_fingerprint").HasMaxLength(256); b.Property(x=>x.MeasuredAtUtc).HasColumnName("measured_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasOne<HardwareProfile>().WithMany().HasForeignKey(x=>x.HardwareProfileId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x=>new{x.ProjectId,x.HardwareProfileId,x.MeasuredAtUtc});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: ChangeRiskAssessmentConfiguration
        modelBuilder.Entity<ChangeRiskAssessment>(b =>
        {
            b.ToTable("change_risk_assessments"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64); b.Property(x=>x.RiskScore).HasColumnName("risk_score").HasPrecision(5,2); b.Property(x=>x.RiskLevel).HasColumnName("risk_level").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.ReasonsJson).HasColumnName("reasons_json").HasMaxLength(16000); b.Property(x=>x.AffectedRepositoriesJson).HasColumnName("affected_repositories_json").HasMaxLength(16000); b.Property(x=>x.AssessedAtUtc).HasColumnName("assessed_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.CommitSha,x.AssessedAtUtc});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: RegressionTriageConfiguration
        modelBuilder.Entity<RegressionTriage>(b =>
        {
            b.ToTable("regression_triages"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.IncidentId).HasColumnName("incident_id"); b.Property(x=>x.SuspectedCommit).HasColumnName("suspected_commit").HasMaxLength(64); b.Property(x=>x.SuspectedSubsystem).HasColumnName("suspected_subsystem").HasMaxLength(200); b.Property(x=>x.SuspectedFilesJson).HasColumnName("suspected_files_json").HasMaxLength(16000); b.Property(x=>x.SuggestedOwner).HasColumnName("suggested_owner").HasMaxLength(200); b.Property(x=>x.Confidence).HasColumnName("confidence").HasPrecision(5,2); b.Property(x=>x.EvidenceIdsJson).HasColumnName("evidence_ids_json").HasMaxLength(16000); b.Property(x=>x.Explanation).HasColumnName("explanation").HasMaxLength(4000); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Incident>().WithMany().HasForeignKey(x=>x.IncidentId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc}); b.HasIndex(x=>new{x.IncidentId,x.CreatedAtUtc});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: ReleaseReadinessSnapshotConfiguration
        modelBuilder.Entity<ReleaseReadinessSnapshot>(b =>
        {
            b.ToTable("release_readiness_snapshots"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64); b.Property(x=>x.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.Confidence).HasColumnName("confidence").HasPrecision(5,2); b.Property(x=>x.BlockersJson).HasColumnName("blockers_json").HasMaxLength(16000); b.Property(x=>x.WarningsJson).HasColumnName("warnings_json").HasMaxLength(16000); b.Property(x=>x.RequiredActionsJson).HasColumnName("required_actions_json").HasMaxLength(16000); b.Property(x=>x.Summary).HasColumnName("summary").HasMaxLength(4000); b.Property(x=>x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.EvaluatedAtUtc});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: ExternalWorkItemLinkConfiguration
        modelBuilder.Entity<ExternalWorkItemLink>(b =>
        {
            b.ToTable("external_work_item_links"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.IncidentId).HasColumnName("incident_id"); b.Property(x=>x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.ExternalId).HasColumnName("external_id").HasMaxLength(200); b.Property(x=>x.Url).HasColumnName("url").HasMaxLength(2000); b.Property(x=>x.Title).HasColumnName("title").HasMaxLength(300); b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Incident>().WithMany().HasForeignKey(x=>x.IncidentId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.IncidentId,x.Provider}).IsUnique();
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: ReleaseEvidenceBundleConfiguration
        modelBuilder.Entity<ReleaseEvidenceBundle>(b =>
        {
            b.ToTable("release_evidence_bundles"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64); b.Property(x=>x.BuildId).HasColumnName("build_id").HasMaxLength(200); b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.ManifestArtifactId).HasColumnName("manifest_artifact_id").HasMaxLength(64); b.Property(x=>x.SbomArtifactId).HasColumnName("sbom_artifact_id").HasMaxLength(64); b.Property(x=>x.BundleArtifactId).HasColumnName("bundle_artifact_id").HasMaxLength(64); b.Property(x=>x.ArtifactSha256).HasColumnName("artifact_sha256").HasMaxLength(64); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: RepositoryNodeConfiguration
        modelBuilder.Entity<RepositoryNode>(b =>
        {
            b.ToTable("repository_nodes"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.Key).HasColumnName("key").HasMaxLength(160); b.Property(x=>x.RepositoryUrl).HasColumnName("repository_url").HasMaxLength(1000); b.Property(x=>x.DefaultBranch).HasColumnName("default_branch").HasMaxLength(200); b.Property(x=>x.Role).HasColumnName("role").HasMaxLength(120); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.Key}).IsUnique();
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: RepositoryDependencyConfiguration
        modelBuilder.Entity<RepositoryDependency>(b =>
        {
            b.ToTable("repository_dependencies"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.SourceRepositoryId).HasColumnName("source_repository_id"); b.Property(x=>x.TargetRepositoryId).HasColumnName("target_repository_id"); b.Property(x=>x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.ContractKey).HasColumnName("contract_key").HasMaxLength(200); b.Property(x=>x.Critical).HasColumnName("critical"); b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasOne<RepositoryNode>().WithMany().HasForeignKey(x=>x.SourceRepositoryId).OnDelete(DeleteBehavior.Cascade); b.HasOne<RepositoryNode>().WithMany().HasForeignKey(x=>x.TargetRepositoryId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.SourceRepositoryId,x.TargetRepositoryId,x.Kind,x.ContractKey}).IsUnique();
        });

        // Frozen from DevelopmentIntelligenceConfigurations.cs: CrossRepositoryImpactConfiguration
        modelBuilder.Entity<CrossRepositoryImpact>(b =>
        {
            b.ToTable("cross_repository_impacts"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64); b.Property(x=>x.SourceRepositoryId).HasColumnName("source_repository_id"); b.Property(x=>x.Level).HasColumnName("level").HasConversion<string>().HasMaxLength(40); b.Property(x=>x.AffectedRepositoryIdsJson).HasColumnName("affected_repository_ids_json").HasMaxLength(16000); b.Property(x=>x.ReasonsJson).HasColumnName("reasons_json").HasMaxLength(16000); b.Property(x=>x.AnalyzedAtUtc).HasColumnName("analyzed_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade); b.HasOne<RepositoryNode>().WithMany().HasForeignKey(x=>x.SourceRepositoryId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.ProjectId,x.CommitSha,x.AnalyzedAtUtc});
        });

        // Frozen from DevelopmentGraphConfigurations.cs: DevelopmentGraphNodeConfiguration
        modelBuilder.Entity<DevelopmentGraphNode>(b =>
        {
            b.ToTable("development_graph_nodes"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id");
            b.Property(x=>x.Type).HasColumnName("node_type").HasConversion<string>().HasMaxLength(60); b.Property(x=>x.Key).HasColumnName("node_key").HasMaxLength(300); b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(300); b.Property(x=>x.Status).HasColumnName("status").HasMaxLength(80);
            b.Property(x=>x.Provider).HasColumnName("provider").HasMaxLength(80); b.Property(x=>x.ExternalReference).HasColumnName("external_reference").HasMaxLength(2000); b.Property(x=>x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(64000);
            b.Property(x=>x.FirstObservedAtUtc).HasColumnName("first_observed_at_utc"); b.Property(x=>x.LastObservedAtUtc).HasColumnName("last_observed_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x=>new{x.ProjectId,x.Type,x.Key}).IsUnique(); b.HasIndex(x=>new{x.ProjectId,x.Type,x.Status}); b.HasIndex(x=>new{x.ProjectId,x.LastObservedAtUtc});
        });

        // Frozen from DevelopmentGraphConfigurations.cs: DevelopmentGraphEdgeConfiguration
        modelBuilder.Entity<DevelopmentGraphEdge>(b =>
        {
            b.ToTable("development_graph_edges"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.SourceNodeId).HasColumnName("source_node_id"); b.Property(x=>x.TargetNodeId).HasColumnName("target_node_id");
            b.Property(x=>x.Type).HasColumnName("edge_type").HasConversion<string>().HasMaxLength(60); b.Property(x=>x.Weight).HasColumnName("weight").HasPrecision(6,2); b.Property(x=>x.Critical).HasColumnName("critical"); b.Property(x=>x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(32000);
            b.Property(x=>x.FirstObservedAtUtc).HasColumnName("first_observed_at_utc"); b.Property(x=>x.LastObservedAtUtc).HasColumnName("last_observed_at_utc"); b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<DevelopmentGraphNode>().WithMany().HasForeignKey(x=>x.SourceNodeId).OnDelete(DeleteBehavior.Cascade); b.HasOne<DevelopmentGraphNode>().WithMany().HasForeignKey(x=>x.TargetNodeId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x=>new{x.ProjectId,x.SourceNodeId,x.TargetNodeId,x.Type}).IsUnique(); b.HasIndex(x=>new{x.ProjectId,x.SourceNodeId}); b.HasIndex(x=>new{x.ProjectId,x.TargetNodeId});
        });

        // Frozen from ProjectClaimConfiguration.cs: ProjectClaimConfiguration
        modelBuilder.Entity<ProjectClaim>(b =>
        {
            b.ToTable("project_claims");
                    b.HasKey(x => x.Id);
                    b.Property(x => x.Id).HasColumnName("id");
                    b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
                    b.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
                    b.Property(x => x.ClaimedStatus).HasColumnName("claimed_status").HasMaxLength(120).IsRequired();
                    b.Property(x => x.ClaimedProgress).HasColumnName("claimed_progress").HasPrecision(5, 2);
                    b.Property(x => x.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(200).IsRequired();
                    b.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000);
                    b.Property(x => x.ClaimedAtUtc).HasColumnName("claimed_at_utc").IsRequired();
                    b.Property(x => x.VerificationStatus).HasColumnName("verification_status").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.LastVerifiedAtUtc).HasColumnName("last_verified_at_utc");
            
                    b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.ClaimedAtUtc });
        });

        // Frozen from ProjectTruthSnapshotConfiguration.cs: ProjectTruthSnapshotConfiguration
        modelBuilder.Entity<ProjectTruthSnapshot>(b =>
        {
            b.ToTable("project_truth_snapshots");
                    b.HasKey(x => x.Id);
                    b.Property(x => x.Id).HasColumnName("id");
                    b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
                    b.Property(x => x.TruthStatus).HasColumnName("truth_status").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.RiskLevel).HasColumnName("risk_level").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.HealthScore).HasColumnName("health_score").HasPrecision(5, 2).IsRequired();
                    b.Property(x => x.VerifiedProgress).HasColumnName("verified_progress").HasPrecision(5, 2).IsRequired();
                    b.Property(x => x.ReportedProgress).HasColumnName("reported_progress").HasPrecision(5, 2);
                    b.Property(x => x.EvidenceConfidence).HasColumnName("evidence_confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x => x.BlockerCount).HasColumnName("blocker_count").IsRequired();
                    b.Property(x => x.ContradictionCount).HasColumnName("contradiction_count").IsRequired();
                    b.Property(x => x.UnverifiedClaimCount).HasColumnName("unverified_claim_count").IsRequired();
                    b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
                    b.Property(x => x.BlockersJson).HasColumnName("blockers_json").HasMaxLength(32000).IsRequired();
                    b.Property(x => x.TopActionsJson).HasColumnName("top_actions_json").HasMaxLength(16000).IsRequired();
                    b.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc").IsRequired();
            
                    b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
        });

        // Frozen from QaFindingConfiguration.cs: QaFindingConfiguration
        modelBuilder.Entity<QaFinding>(b =>
        {
            b.ToTable("qa_findings"); b.HasKey(x=>x.Id);
                    b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.QaRunId).HasColumnName("qa_run_id");
                    b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
                    b.Property(x=>x.Language).HasColumnName("language").HasConversion(v=>v.Value,v=>LanguageId.Parse(v)).HasMaxLength(40).IsRequired();
                    b.Property(x=>x.Category).HasColumnName("category").HasMaxLength(120).IsRequired();
                    b.Property(x=>x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x=>x.Tool).HasColumnName("tool").HasMaxLength(120).IsRequired();
                    b.Property(x=>x.Title).HasColumnName("title").HasMaxLength(240).IsRequired();
                    b.Property(x=>x.Message).HasColumnName("message").HasMaxLength(8000).IsRequired();
                    b.Property(x=>x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64).IsRequired();
                    b.Property(x=>x.FilePath).HasColumnName("file_path").HasMaxLength(1000); b.Property(x=>x.Line).HasColumnName("line"); b.Property(x=>x.Column).HasColumnName("column");
                    b.Property(x=>x.Function).HasColumnName("function").HasMaxLength(500); b.Property(x=>x.ExceptionType).HasColumnName("exception_type").HasMaxLength(300);
                    b.Property(x=>x.StackTrace).HasColumnName("stack_trace").HasMaxLength(32000); b.Property(x=>x.RuleId).HasColumnName("rule_id").HasMaxLength(300);
                    b.Property(x=>x.ReproductionStatus).HasColumnName("reproduction_status").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x=>x.ReproducedAttempts).HasColumnName("reproduced_attempts"); b.Property(x=>x.TotalAttempts).HasColumnName("total_attempts"); b.Property(x=>x.ObservedAtUtc).HasColumnName("observed_at_utc");
                    b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasOne<QaRun>().WithMany().HasForeignKey(x=>x.QaRunId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x=>new{x.ProjectId,x.Fingerprint,x.ObservedAtUtc});
        });

        // Frozen from QaRunConfiguration.cs: QaRunConfiguration
        modelBuilder.Entity<QaRun>(b =>
        {
            b.ToTable("qa_runs"); b.HasKey(x=>x.Id);
                    b.Property(x=>x.Id).HasColumnName("id");
                    b.Property(x=>x.ProjectId).HasColumnName("project_id");
                    b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
                    b.Property(x=>x.Language).HasColumnName("language").HasConversion(v=>v.Value,v=>LanguageId.Parse(v)).HasMaxLength(40).IsRequired();
                    b.Property(x=>x.WorkspaceRelativePath).HasColumnName("workspace_relative_path").HasMaxLength(500).IsRequired();
                    b.Property(x=>x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
                    b.Property(x=>x.ArtifactRoot).HasColumnName("artifact_root").HasMaxLength(1000).IsRequired();
                    b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
                    b.Property(x=>x.FindingCount).HasColumnName("finding_count");
                    b.Property(x=>x.CriticalFindingCount).HasColumnName("critical_finding_count");
                    b.Property(x=>x.WarningFindingCount).HasColumnName("warning_finding_count");
                    b.Property(x=>x.SummaryJson).HasColumnName("summary_json").HasMaxLength(32000);
                    b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");
                    b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc");
                    b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc");
                    b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});
        });

        // Frozen from ReproductionAttemptConfiguration.cs: ReproductionAttemptConfiguration
        modelBuilder.Entity<ReproductionAttempt>(b =>
        {
            b.ToTable("reproduction_attempts"); b.HasKey(x=>x.Id);
                    b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.QaFindingId).HasColumnName("qa_finding_id");
                    b.Property(x=>x.AttemptNumber).HasColumnName("attempt_number"); b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
                    b.Property(x=>x.Reproduced).HasColumnName("reproduced"); b.Property(x=>x.Crashed).HasColumnName("crashed"); b.Property(x=>x.TimedOut).HasColumnName("timed_out"); b.Property(x=>x.ExitCode).HasColumnName("exit_code"); b.Property(x=>x.DurationMs).HasColumnName("duration_ms");
                    b.Property(x=>x.ArtifactDirectory).HasColumnName("artifact_directory").HasMaxLength(1000).IsRequired(); b.Property(x=>x.VideoPath).HasColumnName("video_path").HasMaxLength(1000); b.Property(x=>x.AudioPath).HasColumnName("audio_path").HasMaxLength(1000); b.Property(x=>x.DumpPath).HasColumnName("dump_path").HasMaxLength(1000); b.Property(x=>x.ResultJson).HasColumnName("result_json").HasMaxLength(32000);
                    b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc"); b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc");
                    b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                    b.HasOne<QaFinding>().WithMany().HasForeignKey(x=>x.QaFindingId).OnDelete(DeleteBehavior.Cascade);
                    b.HasIndex(x=>new{x.QaFindingId,x.AttemptNumber}).IsUnique();
        });
    }
}
