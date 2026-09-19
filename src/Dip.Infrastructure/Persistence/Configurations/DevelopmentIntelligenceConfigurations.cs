using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for game replay sessions.
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
