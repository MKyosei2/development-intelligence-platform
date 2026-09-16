// Purpose: Implements P ip el in eR un Co nf ig ur at io n persistence behavior.
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class PipelineRunConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> builder)
    {
        // Keep this mapping identical to the formal legacy baseline. This
        // configuration and PipelineRunCoreConfiguration are both discovered by
        // ApplyConfigurationsFromAssembly, so conflicting mappings would produce
        // a model/schema mismatch at runtime.
        builder.ToTable("PipelineRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CommitSha)
            .HasConversion(
                sha => sha.Value,
                value => new CommitSha(value))
            .HasColumnName("CommitSha")
            .HasMaxLength(64)
            .IsRequired();

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

        builder.Property(x => x.FailureReason)
            .HasMaxLength(2000);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

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
