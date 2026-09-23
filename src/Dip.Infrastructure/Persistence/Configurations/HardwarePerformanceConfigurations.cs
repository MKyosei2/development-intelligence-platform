// Purpose: Maps hardware profiles, community measurements, simulations, backtests, and performance budgets to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class HardwareProfileConfiguration : IEntityTypeConfiguration<HardwareProfile>
{
    public void Configure(EntityTypeBuilder<HardwareProfile> b)
    {
        b.ToTable("hardware_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        b.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(80).IsRequired();
        b.Property(x => x.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.CpuSingleScore).HasColumnName("cpu_single_score").HasPrecision(12, 3);
        b.Property(x => x.CpuMultiScore).HasColumnName("cpu_multi_score").HasPrecision(12, 3);
        b.Property(x => x.GpuScore).HasColumnName("gpu_score").HasPrecision(12, 3);
        b.Property(x => x.MemoryScore).HasColumnName("memory_score").HasPrecision(12, 3);
        b.Property(x => x.StorageScore).HasColumnName("storage_score").HasPrecision(12, 3);
        b.Property(x => x.ThermalScore).HasColumnName("thermal_score").HasPrecision(12, 3);
        b.Property(x => x.MemoryBudgetMb).HasColumnName("memory_budget_mb");
        b.Property(x => x.TargetFps).HasColumnName("target_fps");
        b.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(32000).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasIndex(x => new { x.Name, x.Platform }).IsUnique();
    }
}

public sealed class HardwareCalibrationConfiguration : IEntityTypeConfiguration<HardwareCalibration>
{
    public void Configure(EntityTypeBuilder<HardwareCalibration> b)
    {
        b.ToTable("hardware_calibrations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.MachineFingerprint).HasColumnName("machine_fingerprint").HasMaxLength(64).IsRequired();
        b.Property(x => x.CpuSingleScore).HasColumnName("cpu_single_score").HasPrecision(12, 3);
        b.Property(x => x.CpuMultiScore).HasColumnName("cpu_multi_score").HasPrecision(12, 3);
        b.Property(x => x.GpuScore).HasColumnName("gpu_score").HasPrecision(12, 3);
        b.Property(x => x.MemoryScore).HasColumnName("memory_score").HasPrecision(12, 3);
        b.Property(x => x.StorageScore).HasColumnName("storage_score").HasPrecision(12, 3);
        b.Property(x => x.AvailableMemoryMb).HasColumnName("available_memory_mb");
        b.Property(x => x.EnvironmentJson).HasColumnName("environment_json").HasMaxLength(32000).IsRequired();
        b.Property(x => x.MeasuredAtUtc).HasColumnName("measured_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.MeasuredAtUtc });
    }
}

public sealed class HardwareBacktestConfiguration : IEntityTypeConfiguration<HardwareBacktest>
{
    public void Configure(EntityTypeBuilder<HardwareBacktest> b)
    {
        b.ToTable("hardware_backtests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.HardwareProfileId).HasColumnName("hardware_profile_id");
        b.Property(x => x.ScenarioKey).HasColumnName("scenario_key").HasMaxLength(200);
        b.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x => x.PredictedFps).HasColumnName("predicted_fps").HasPrecision(12, 3);
        b.Property(x => x.ActualFps).HasColumnName("actual_fps").HasPrecision(12, 3);
        b.Property(x => x.PredictionErrorPercent).HasColumnName("prediction_error_percent").HasPrecision(12, 3);
        b.Property(x => x.EnvironmentFingerprint).HasColumnName("environment_fingerprint").HasMaxLength(256);
        b.Property(x => x.MeasuredAtUtc).HasColumnName("measured_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<HardwareProfile>().WithMany().HasForeignKey(x => x.HardwareProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.HardwareProfileId, x.MeasuredAtUtc });
    }
}

public sealed class HardwareCommunitySampleConfiguration : IEntityTypeConfiguration<HardwareCommunitySample>
{
    public void Configure(EntityTypeBuilder<HardwareCommunitySample> builder)
    {
        builder.ToTable("hardware_community_samples");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProfileKey).HasColumnName("profile_key").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(80).IsRequired();
        builder.Property(x => x.HardwareKeyHash).HasColumnName("hardware_key_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.OsFamily).HasColumnName("os_family").HasMaxLength(120).IsRequired();
        builder.Property(x => x.DriverFamily).HasColumnName("driver_family").HasMaxLength(160).IsRequired();
        builder.Property(x => x.CpuSingleScore).HasColumnName("cpu_single_score").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.CpuMultiScore).HasColumnName("cpu_multi_score").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.GpuScore).HasColumnName("gpu_score").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.MemoryScore).HasColumnName("memory_score").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.StorageScore).HasColumnName("storage_score").HasPrecision(12, 3).IsRequired();
        builder.Property(x => x.ThermalRetentionPercent).HasColumnName("thermal_retention_percent").HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.AvailableMemoryMb).HasColumnName("available_memory_mb").IsRequired();
        builder.Property(x => x.RealDevice).HasColumnName("real_device").IsRequired();
        builder.Property(x => x.ConsentGranted).HasColumnName("consent_granted").IsRequired();
        builder.Property(x => x.MeasuredAtUtc).HasColumnName("measured_at_utc").IsRequired();
        builder.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc").IsRequired();
        builder.HasIndex(x => new { x.ProfileKey, x.Platform, x.ReceivedAtUtc });
        builder.HasIndex(x => new { x.HardwareKeyHash, x.MeasuredAtUtc });
    }
}

public sealed class CommunityHardwareSampleConfiguration : IEntityTypeConfiguration<CommunityHardwareSample>
{
    public void Configure(EntityTypeBuilder<CommunityHardwareSample> b)
    {
        b.ToTable("community_hardware_samples");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HardwareKey).HasColumnName("hardware_key").HasMaxLength(160);
        b.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(80);
        b.Property(x => x.CpuArch).HasColumnName("cpu_arch").HasMaxLength(80);
        b.Property(x => x.GpuVendor).HasColumnName("gpu_vendor").HasMaxLength(80);
        b.Property(x => x.OsFamily).HasColumnName("os_family").HasMaxLength(80);
        b.Property(x => x.DriverFamily).HasColumnName("driver_family").HasMaxLength(160);
        b.Property(x => x.CpuSingleScore).HasColumnName("cpu_single_score");
        b.Property(x => x.CpuMultiScore).HasColumnName("cpu_multi_score");
        b.Property(x => x.GpuScore).HasColumnName("gpu_score");
        b.Property(x => x.MemoryScore).HasColumnName("memory_score");
        b.Property(x => x.StorageScore).HasColumnName("storage_score");
        b.Property(x => x.ThermalScore).HasColumnName("thermal_score");
        b.Property(x => x.MemoryMb).HasColumnName("memory_mb");
        b.Property(x => x.SourceFingerprint).HasColumnName("source_fingerprint").HasMaxLength(64);
        b.Property(x => x.MeasuredAtUtc).HasColumnName("measured_at_utc");
        b.HasIndex(x => new { x.HardwareKey, x.MeasuredAtUtc });
    }
}

public sealed class PerformanceSimulationRunConfiguration : IEntityTypeConfiguration<PerformanceSimulationRun>
{
    public void Configure(EntityTypeBuilder<PerformanceSimulationRun> b)
    {
        b.ToTable("performance_simulation_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.HardwareProfileId).HasColumnName("hardware_profile_id");
        b.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        b.Property(x => x.ArtifactDirectory).HasColumnName("artifact_directory").HasMaxLength(1000).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ConfidencePercent).HasColumnName("confidence_percent").HasPrecision(5, 2);
        b.Property(x => x.ResultJson).HasColumnName("result_json").HasMaxLength(32000);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        b.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<HardwareProfile>().WithMany().HasForeignKey(x => x.HardwareProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}

public sealed class PerformanceBudgetProfileConfiguration : IEntityTypeConfiguration<PerformanceBudgetProfile>
{
    public void Configure(EntityTypeBuilder<PerformanceBudgetProfile> b)
    {
        b.ToTable("performance_budget_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Key).HasColumnName("key").HasMaxLength(120);
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        b.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(80);
        b.Property(x => x.TargetProfile).HasColumnName("target_profile").HasMaxLength(120);
        b.Property(x => x.RulesJson).HasColumnName("rules_json").HasMaxLength(32000);
        b.Property(x => x.Enabled).HasColumnName("enabled");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
        b.HasIndex(x => x.Key).IsUnique().HasFilter("project_id IS NULL");
    }
}

public sealed class PerformanceBudgetEvaluationConfiguration : IEntityTypeConfiguration<PerformanceBudgetEvaluation>
{
    public void Configure(EntityTypeBuilder<PerformanceBudgetEvaluation> b)
    {
        b.ToTable("performance_budget_evaluations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.ProfileId).HasColumnName("profile_id");
        b.Property(x => x.ReplayRunId).HasColumnName("replay_run_id");
        b.Property(x => x.BenchmarkRunId).HasColumnName("benchmark_run_id");
        b.Property(x => x.Verdict).HasColumnName("verdict").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(2000);
        b.Property(x => x.DetailJson).HasColumnName("detail_json").HasMaxLength(32000);
        b.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PerformanceBudgetProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GameReplayRun>().WithMany().HasForeignKey(x => x.ReplayRunId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<BenchmarkRun>().WithMany().HasForeignKey(x => x.BenchmarkRunId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
    }
}
