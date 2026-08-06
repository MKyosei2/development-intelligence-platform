using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class PipelineRunConfiguration :
    IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(
        EntityTypeBuilder<PipelineRun> builder)
    {
        builder.ToTable("pipeline_runs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalRunId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Branch)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CollectorType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CollectorVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(2000);

        // CommitSha is a value object, not an owned entity.
        builder.Property(x => x.CommitSha)
            .HasConversion(
                sha => sha.Value,
                value => new CommitSha(value))
            .HasColumnName("commit_sha")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.ExternalRunId,
            x.CollectorType,
            x.AttemptNumber
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.QueuedAtUtc
        });
    }
}
