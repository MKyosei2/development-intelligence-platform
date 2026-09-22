// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

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
