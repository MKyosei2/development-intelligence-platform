// Purpose: Implements E vi de nc eR ec or dC on fi gu ra ti on persistence behavior.
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

        builder.Property(x => x.SourceInstanceId).HasColumnName("source_instance_id").HasMaxLength(200);
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(300);
        builder.Property(x => x.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(80).HasDefaultValue(Dip.Domain.Enums.EvidenceEventType.Unknown).IsRequired();
        builder.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(300);
        builder.Property(x => x.MachineId).HasColumnName("machine_id").HasMaxLength(300);
        builder.Property(x => x.ArtifactId).HasColumnName("artifact_id").HasMaxLength(300);
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(128);
        builder.Property(x => x.ParentEvidenceId).HasColumnName("parent_evidence_id");
        builder.Property(x => x.PrivacyClass).HasColumnName("privacy_class").HasConversion<string>().HasMaxLength(40).HasDefaultValue(Dip.Domain.Enums.EvidencePrivacyClass.Internal).IsRequired();
        builder.Property(x => x.IntegrityStatus).HasColumnName("integrity_status").HasConversion<string>().HasMaxLength(40).HasDefaultValue(Dip.Domain.Enums.EvidenceIntegrityStatus.LegacyUnsigned).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasMaxLength(32000);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ProjectId, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.EvidenceKind });
        builder.HasIndex(x => new { x.ProjectId, x.EventType, x.ObservedAtUtc });
        builder.HasIndex(x => new { x.ProjectId, x.SourceType, x.SourceInstanceId });
        builder.HasIndex(x => new { x.ProjectId, x.SourceType, x.SourceInstanceId, x.SourceEventId })
            .IsUnique()
            .HasFilter("\"source_event_id\" IS NOT NULL AND \"source_instance_id\" IS NOT NULL");
    }
}
