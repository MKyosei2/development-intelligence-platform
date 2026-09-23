// Purpose: Maps game replay sessions, runs, and replay metrics to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class GameReplaySessionConfiguration : IEntityTypeConfiguration<GameReplaySession>
{
    public void Configure(EntityTypeBuilder<GameReplaySession> b)
    {
        b.ToTable("game_replay_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Key).HasColumnName("key").HasMaxLength(120);
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        b.Property(x => x.Scene).HasColumnName("scene").HasMaxLength(300);
        b.Property(x => x.InputArtifactId).HasColumnName("input_artifact_id").HasMaxLength(1000);
        b.Property(x => x.RandomSeed).HasColumnName("random_seed");
        b.Property(x => x.DurationSeconds).HasColumnName("duration_seconds");
        b.Property(x => x.LaunchCommand).HasColumnName("launch_command").HasMaxLength(1000);
        b.Property(x => x.Arguments).HasColumnName("arguments").HasMaxLength(4000);
        b.Property(x => x.WorkingDirectory).HasColumnName("working_directory").HasMaxLength(1000);
        b.Property(x => x.BaselineCommit).HasColumnName("baseline_commit").HasMaxLength(64);
        b.Property(x => x.Enabled).HasColumnName("enabled");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
    }
}

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

public sealed class GameReplayMetricConfiguration : IEntityTypeConfiguration<GameReplayMetric>
{
    public void Configure(EntityTypeBuilder<GameReplayMetric> b)
    {
        b.ToTable("game_replay_metrics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ReplayRunId).HasColumnName("replay_run_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(160);
        b.Property(x => x.Value).HasColumnName("value").HasPrecision(20, 6);
        b.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(40);
        b.Property(x => x.Aggregation).HasColumnName("aggregation").HasMaxLength(40);
        b.HasOne<GameReplayRun>().WithMany().HasForeignKey(x => x.ReplayRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ReplayRunId, x.Name, x.Aggregation });
    }
}
