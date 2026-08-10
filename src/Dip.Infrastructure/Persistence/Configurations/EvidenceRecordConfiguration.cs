using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class EvidenceRecordConfiguration : IEntityTypeConfiguration<EvidenceRecord>
{
    public void Configure(EntityTypeBuilder<EvidenceRecord> builder)
    {
        builder.ToTable("evidence_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.PipelineRunId).HasColumnName("pipeline_run_id");
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.EvidenceKind).HasColumnName("evidence_kind").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.SourceReference).HasColumnName("source_reference").HasMaxLength(1000);
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasMaxLength(16000);
        builder.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.ProjectId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.EvidenceKind });
    }
}
