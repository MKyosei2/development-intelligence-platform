// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;
public sealed class HardwareProfileConfiguration : IEntityTypeConfiguration<HardwareProfile>
{
    public void Configure(EntityTypeBuilder<HardwareProfile> b) { b.ToTable("hardware_profiles");
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
