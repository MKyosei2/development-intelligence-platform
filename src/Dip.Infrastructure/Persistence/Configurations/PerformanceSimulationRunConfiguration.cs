// Purpose: Implements P er fo rm an ce Si mu la ti on Ru nC on fi gu ra ti on persistence behavior.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;
public sealed class PerformanceSimulationRunConfiguration:IEntityTypeConfiguration<PerformanceSimulationRun>
{
 public void Configure(EntityTypeBuilder<PerformanceSimulationRun>b){b.ToTable("performance_simulation_runs");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("id");b.Property(x=>x.ProjectId).HasColumnName("project_id");b.Property(x=>x.HardwareProfileId).HasColumnName("hardware_profile_id");b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();b.Property(x=>x.ArtifactDirectory).HasColumnName("artifact_directory").HasMaxLength(1000).IsRequired();b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);b.Property(x=>x.ConfidencePercent).HasColumnName("confidence_percent").HasPrecision(5,2);b.Property(x=>x.ResultJson).HasColumnName("result_json").HasMaxLength(32000);b.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc");b.Property(x=>x.StartedAtUtc).HasColumnName("started_at_utc");b.Property(x=>x.FinishedAtUtc).HasColumnName("finished_at_utc");b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);b.HasOne<HardwareProfile>().WithMany().HasForeignKey(x=>x.HardwareProfileId).OnDelete(DeleteBehavior.Restrict);b.HasIndex(x=>new{x.ProjectId,x.CreatedAtUtc});}
}
