// Purpose: Freezes the legacy pre-migration core schema so formal migrations remain compatible with databases created by EnsureCreated.
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

/// <summary>
/// Freezes the legacy pre-migrations core schema so the formal migration baseline
/// matches databases previously created by EnsureCreated.
/// </summary>
public sealed class ProjectCoreConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.RepositoryUrl).HasMaxLength(2000).IsRequired();
        b.Property(x => x.DefaultBranch).HasMaxLength(200).IsRequired();
        b.Property(x => x.HealthScore).HasConversion(v => v.Value, v => new HealthScore(v)).HasPrecision(5, 2).IsRequired();
        b.HasIndex(x => x.RepositoryUrl);
    }
}

public sealed class PipelineRunCoreConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> b)
    {
        b.ToTable("PipelineRuns");
        b.HasKey(x => x.Id);
        b.Property(x => x.CommitSha).HasConversion(v => v.Value, v => new CommitSha(v)).HasMaxLength(64).IsRequired();
        b.Property(x => x.ExternalRunId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Branch).HasMaxLength(200).IsRequired();
        b.Property(x => x.CollectorType).HasMaxLength(100).IsRequired();
        b.Property(x => x.CollectorVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.FailureReason).HasMaxLength(2000);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.ExternalRunId, x.CollectorType, x.AttemptNumber }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.QueuedAtUtc });
    }
}

public sealed class MetricCoreConfiguration : IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> b)
    {
        b.ToTable("Metrics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        b.Property(x => x.Value).HasPrecision(18, 6);
        b.Property(x => x.BaselineValue).HasPrecision(18, 6);
        b.Property(x => x.Difference).HasPrecision(18, 6);
        b.Property(x => x.DifferencePercent).HasPrecision(12, 4);
        b.Property(x => x.EnvironmentId).HasMaxLength(120);
        b.Property(x => x.ScenarioId).HasMaxLength(120);
        b.Property(x => x.Platform).HasMaxLength(80);
        b.Property(x => x.BuildConfiguration).HasMaxLength(80);
        b.Property(x => x.BenchmarkVersion).HasMaxLength(80);
        b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.PipelineRunId, x.Name });
    }
}

public sealed class QualityIssueCoreConfiguration : IEntityTypeConfiguration<QualityIssue>
{
    public void Configure(EntityTypeBuilder<QualityIssue> b)
    {
        b.ToTable("QualityIssues");
        b.HasKey(x => x.Id);
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.Category).HasMaxLength(80).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Source).HasMaxLength(1000);
        b.Property(x => x.RuleId).HasMaxLength(200);
        b.Property(x => x.EvidenceJson).HasMaxLength(16000);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Fingerprint });
        b.HasIndex(x => new { x.ProjectId, x.LastSeenAtUtc });
    }
}

public sealed class QualityGateResultCoreConfiguration : IEntityTypeConfiguration<QualityGateResult>
{
    public void Configure(EntityTypeBuilder<QualityGateResult> b)
    {
        b.ToTable("QualityGateResults");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.Score).HasPrecision(5, 2);
        b.Property(x => x.ReasonsJson).HasMaxLength(16000).IsRequired();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.EvaluatedAtUtc });
    }
}

public sealed class OutboxMessageCoreConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.MessageType).HasMaxLength(500).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
        b.Property(x => x.CausationId).HasMaxLength(200);
        b.Property(x => x.LastError).HasMaxLength(4000);
        b.HasIndex(x => new { x.ProcessedAtUtc, x.NextAttemptAtUtc, x.LockedUntilUtc });
    }
}

public sealed class ProcessedWebhookCoreConfiguration : IEntityTypeConfiguration<ProcessedWebhook>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhook> b)
    {
        b.ToTable("ProcessedWebhooks");
        b.HasKey(x => x.DeliveryId);
        b.Property(x => x.DeliveryId).HasMaxLength(200);
        b.Property(x => x.EventName).HasMaxLength(100).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(4000);
        b.HasIndex(x => x.ReceivedAtUtc);
    }
}
