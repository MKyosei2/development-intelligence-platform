// Purpose: Maps project-intelligence runs, forecasts, and persisted intelligence records to PostgreSQL.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class IntelligenceRunConfiguration : IEntityTypeConfiguration<IntelligenceRun>
{
    public void Configure(EntityTypeBuilder<IntelligenceRun> b)
    {
        b.ToTable("intelligence_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Type).HasColumnName("type").HasConversion<string>();
        b.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200);
        b.Property(x => x.ArtifactRoot).HasColumnName("artifact_root").HasMaxLength(1000);
        b.Property(x => x.InputJson).HasColumnName("input_json").HasMaxLength(32000);
        b.Property(x => x.ResultJson).HasColumnName("result_json").HasMaxLength(32000);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        b.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}

public sealed class ProjectForecastConfiguration : IEntityTypeConfiguration<ProjectForecast>
{
    public void Configure(EntityTypeBuilder<ProjectForecast> b)
    {
        b.ToTable("project_forecasts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.ForecastType).HasColumnName("forecast_type").HasMaxLength(80);
        b.Property(x => x.RiskPercent).HasColumnName("risk_percent");
        b.Property(x => x.EarliestUtc).HasColumnName("earliest_utc");
        b.Property(x => x.LatestUtc).HasColumnName("latest_utc");
        b.Property(x => x.Confidence).HasColumnName("confidence").HasConversion<string>();
        b.Property(x => x.RationaleJson).HasColumnName("rationale_json").HasMaxLength(32000);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}

public sealed class P2IntelligenceRecordConfiguration : IEntityTypeConfiguration<P2IntelligenceRecord>
{
    public void Configure(EntityTypeBuilder<P2IntelligenceRecord> builder)
    {
        builder.ToTable("p2_intelligence_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Score).HasColumnName("score").HasPrecision(5, 2);
        builder.Property(x => x.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.SourceReference).HasColumnName("source_reference").HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.Kind, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.CreatedAtUtc });
    }
}
