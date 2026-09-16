// Purpose: Implements R ep ro du ct io nA tt em pt Co nf ig ur at io n persistence behavior.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ReproductionAttemptConfiguration : IEntityTypeConfiguration<ReproductionAttempt>
{
    public void Configure(EntityTypeBuilder<ReproductionAttempt> b)
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
    }
}
