using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for performance budget evaluations.
public sealed class PerformanceBudgetEvaluationConfiguration : IEntityTypeConfiguration<PerformanceBudgetEvaluation>
{
    public void Configure(EntityTypeBuilder<PerformanceBudgetEvaluation> b)
    {
        b.ToTable("performance_budget_evaluations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.ProfileId).HasColumnName("profile_id");
        b.Property(x => x.ReplayRunId).HasColumnName("replay_run_id");
        b.Property(x => x.BenchmarkRunId).HasColumnName("benchmark_run_id");
        b.Property(x => x.Verdict).HasColumnName("verdict").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(2000);
        b.Property(x => x.DetailJson).HasColumnName("detail_json").HasMaxLength(32000);
        b.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PerformanceBudgetProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GameReplayRun>().WithMany().HasForeignKey(x => x.ReplayRunId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<BenchmarkRun>().WithMany().HasForeignKey(x => x.BenchmarkRunId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
    }
}
