using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;
public sealed class QualityIssueConfiguration:IEntityTypeConfiguration<QualityIssue>
{
    public void Configure(EntityTypeBuilder<QualityIssue> b)
    {
        b.ToTable("quality_issues");b.HasKey(x=>x.Id);
        b.Property(x=>x.Fingerprint).HasMaxLength(64).IsRequired();
        b.Property(x=>x.Category).HasMaxLength(80).IsRequired();
        b.Property(x=>x.Title).HasMaxLength(200).IsRequired();
        b.Property(x=>x.Description).HasMaxLength(4000).IsRequired();
        b.Property(x=>x.Source).HasMaxLength(1000);b.Property(x=>x.RuleId).HasMaxLength(200);
        b.Property(x=>x.EvidenceJson).HasColumnType("jsonb");
        b.HasIndex(x=>new{x.ProjectId,x.Fingerprint,x.Status});
        b.HasIndex(x=>new{x.ProjectId,x.Severity,x.Status});
        b.Property<uint>("xmin").IsRowVersion();
    }
}
