using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class P2IntelligenceRecordConfiguration : IEntityTypeConfiguration<P2IntelligenceRecord>
{
    public void Configure(EntityTypeBuilder<P2IntelligenceRecord> builder)
    {
        builder.ToTable("p2_intelligence_records");
        builder.HasKey(x=>x.Id);
        builder.Property(x=>x.Id).HasColumnName("id");
        builder.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        builder.Property(x=>x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(x=>x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x=>x.Score).HasColumnName("score").HasPrecision(5,2);
        builder.Property(x=>x.Confidence).HasColumnName("confidence").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x=>x.Summary).HasColumnName("summary").HasMaxLength(4000).IsRequired();
        builder.Property(x=>x.PayloadJson).HasColumnName("payload_json").HasMaxLength(32000).IsRequired();
        builder.Property(x=>x.SourceReference).HasColumnName("source_reference").HasMaxLength(1000);
        builder.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x=>x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x=>new{x.ProjectId,x.Kind,x.CreatedAtUtc});
        builder.HasIndex(x=>new{x.ProjectId,x.SubjectKey,x.CreatedAtUtc});
    }
}
