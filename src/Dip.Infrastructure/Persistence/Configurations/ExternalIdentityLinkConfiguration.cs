// Purpose: Configures persistence mapping for ExternalIdentityLink.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ExternalIdentityLinkConfiguration : IEntityTypeConfiguration<ExternalIdentityLink> { public void Configure(EntityTypeBuilder<ExternalIdentityLink> b) { b.ToTable("external_identity_links");
 b.HasKey(x => x.Id);
 b.Property(x => x.Provider).HasMaxLength(120);
 b.Property(x => x.ExternalId).HasMaxLength(300);
 b.Property(x => x.ApprovedBy).HasMaxLength(240);
 b.HasIndex(x => new { x.Provider, x.ExternalId });
 b.HasIndex(x => x.IdentityProfileId);
 } }
