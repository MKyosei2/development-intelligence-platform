using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;
public sealed class PipelineRunConfiguration:IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> b)
    {
        b.ToTable("pipeline_runs");b.HasKey(x=>x.Id);
        b.Property(x=>x.ExternalRunId).HasMaxLength(200).IsRequired();
        b.Property(x=>x.Branch).HasMaxLength(200).IsRequired();
        b.Property(x=>x.CollectorType).HasMaxLength(100).IsRequired();
        b.Property(x=>x.CollectorVersion).HasMaxLength(50).IsRequired();
        b.Property(x=>x.FailureReason).HasMaxLength(2000);
        b.HasIndex(x=>new{x.ProjectId,x.ExternalRunId,x.CollectorType,x.AttemptNumber}).IsUnique();
        b.HasIndex(x=>new{x.ProjectId,x.FinishedAtUtc});
        b.Property<uint>("xmin").IsRowVersion();
    }
}
