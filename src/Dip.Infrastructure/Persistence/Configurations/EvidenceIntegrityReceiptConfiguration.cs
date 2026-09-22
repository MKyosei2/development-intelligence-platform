// Purpose: Configures persistence mapping for EvidenceIntegrityReceipt.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class EvidenceIntegrityReceiptConfiguration : IEntityTypeConfiguration<EvidenceIntegrityReceipt> { public void Configure(EntityTypeBuilder<EvidenceIntegrityReceipt> b) { b.ToTable("evidence_integrity_receipts");
 b.HasKey(x => x.Id);
 b.Property(x => x.PayloadHash).HasMaxLength(128);
 b.Property(x => x.PreviousHash).HasMaxLength(128);
 b.Property(x => x.ChainHash).HasMaxLength(128);
 b.Property(x => x.SourceSignature).HasMaxLength(16000);
 b.Property(x => x.SourceKeyId).HasMaxLength(240);
 b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
 b.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.IngestedAtUtc });
 b.HasIndex(x => x.EvidenceId);
 } }
