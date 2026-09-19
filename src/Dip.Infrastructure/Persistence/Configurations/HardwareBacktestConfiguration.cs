using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for hardware backtests.
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
