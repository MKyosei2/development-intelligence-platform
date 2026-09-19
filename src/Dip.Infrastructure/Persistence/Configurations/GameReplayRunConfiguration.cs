using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for game replay runs.
public sealed class GameReplayRunConfiguration : IEntityTypeConfiguration<GameReplayRun>
{
    public void Configure(EntityTypeBuilder<GameReplayRun> b)
    {
        b.ToTable("game_replay_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.SessionId).HasColumnName("session_id");
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        b.Property(x => x.BuildId).HasColumnName("build_id").HasMaxLength(200);
        b.Property(x => x.HardwareFingerprint).HasColumnName("hardware_fingerprint").HasMaxLength(256);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.SummaryJson).HasColumnName("summary_json").HasMaxLength(32000);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        b.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<GameReplaySession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<DurableJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.JobId).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.SessionId, x.HardwareFingerprint, x.FinishedAtUtc });
    }
}
