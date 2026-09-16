// Purpose: Implements C la im Ev id en ce Li nk Co nf ig ur at io n persistence behavior.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ClaimEvidenceLinkConfiguration : IEntityTypeConfiguration<ClaimEvidenceLink>
{
    public void Configure(EntityTypeBuilder<ClaimEvidenceLink> builder)
    {
        builder.ToTable("claim_evidence_links");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ClaimId).HasColumnName("claim_id").IsRequired();
        builder.Property(x => x.EvidenceRecordId).HasColumnName("evidence_record_id").IsRequired();
        builder.Property(x => x.Relation).HasColumnName("relation").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<ProjectClaim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EvidenceRecord>().WithMany().HasForeignKey(x => x.EvidenceRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ClaimId, x.EvidenceRecordId }).IsUnique();
    }
}
