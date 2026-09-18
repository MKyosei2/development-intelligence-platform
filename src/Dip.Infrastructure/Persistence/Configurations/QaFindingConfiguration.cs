// Purpose: Configures EF Core persistence for Q aF in di ng Co nf ig ur at io n.
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class QaFindingConfiguration : IEntityTypeConfiguration<QaFinding>
{
    public void Configure(EntityTypeBuilder<QaFinding> b)
    {
        b.ToTable("qa_findings"); b.HasKey(x=>x.Id);
        b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.ProjectId).HasColumnName("project_id"); b.Property(x=>x.QaRunId).HasColumnName("qa_run_id");
        b.Property(x=>x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        b.Property(x=>x.Language).HasColumnName("language").HasConversion(v=>v.Value,v=>LanguageId.Parse(v)).HasMaxLength(40).IsRequired();
        b.Property(x=>x.Category).HasColumnName("category").HasMaxLength(120).IsRequired();
        b.Property(x=>x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x=>x.Tool).HasColumnName("tool").HasMaxLength(120).IsRequired();
        b.Property(x=>x.Title).HasColumnName("title").HasMaxLength(240).IsRequired();
        b.Property(x=>x.Message).HasColumnName("message").HasMaxLength(8000).IsRequired();
        b.Property(x=>x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64).IsRequired();
        b.Property(x=>x.FilePath).HasColumnName("file_path").HasMaxLength(1000); b.Property(x=>x.Line).HasColumnName("line"); b.Property(x=>x.Column).HasColumnName("column");
        b.Property(x=>x.Function).HasColumnName("function").HasMaxLength(500); b.Property(x=>x.ExceptionType).HasColumnName("exception_type").HasMaxLength(300);
        b.Property(x=>x.StackTrace).HasColumnName("stack_trace").HasMaxLength(32000); b.Property(x=>x.RuleId).HasColumnName("rule_id").HasMaxLength(300);
        b.Property(x=>x.DiagnosticSchemaVersion).HasColumnName("diagnostic_schema_version").HasMaxLength(80).IsRequired(); b.Property(x=>x.SourceFormat).HasColumnName("source_format").HasMaxLength(80); b.Property(x=>x.SourceFormatVersion).HasColumnName("source_format_version").HasMaxLength(80);
        b.Property(x=>x.ReproductionStatus).HasColumnName("reproduction_status").HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x=>x.ReproducedAttempts).HasColumnName("reproduced_attempts"); b.Property(x=>x.TotalAttempts).HasColumnName("total_attempts"); b.Property(x=>x.ObservedAtUtc).HasColumnName("observed_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<QaRun>().WithMany().HasForeignKey(x=>x.QaRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x=>new{x.ProjectId,x.Fingerprint,x.ObservedAtUtc});
    }
}
