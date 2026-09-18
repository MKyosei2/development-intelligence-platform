// Purpose: Configures EF Core persistence for H ar dw ar eC al ib ra ti on Co nf ig ur at io n.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;
public sealed class HardwareCalibrationConfiguration:IEntityTypeConfiguration<HardwareCalibration>
{
 public void Configure(EntityTypeBuilder<HardwareCalibration>b){b.ToTable("hardware_calibrations");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.MachineFingerprint).HasColumnName("machine_fingerprint").HasMaxLength(64).IsRequired();b.Property(x=>x.CpuSingleScore).HasColumnName("cpu_single_score").HasPrecision(12,3);b.Property(x=>x.CpuMultiScore).HasColumnName("cpu_multi_score").HasPrecision(12,3);b.Property(x=>x.GpuScore).HasColumnName("gpu_score").HasPrecision(12,3);b.Property(x=>x.MemoryScore).HasColumnName("memory_score").HasPrecision(12,3);b.Property(x=>x.StorageScore).HasColumnName("storage_score").HasPrecision(12,3);b.Property(x=>x.AvailableMemoryMb).HasColumnName("available_memory_mb");b.Property(x=>x.EnvironmentJson).HasColumnName("environment_json").HasMaxLength(32000).IsRequired();b.Property(x=>x.MeasuredAtUtc).HasColumnName("measured_at_utc");b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>new{x.ProjectId,x.MeasuredAtUtc});}
}
