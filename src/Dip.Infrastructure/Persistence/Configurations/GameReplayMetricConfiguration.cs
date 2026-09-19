using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for game replay metrics.
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
