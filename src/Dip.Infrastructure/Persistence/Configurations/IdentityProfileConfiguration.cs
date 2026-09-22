// Purpose: Configures persistence mapping for IdentityProfile.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class IdentityProfileConfiguration : IEntityTypeConfiguration<IdentityProfile> { public void Configure(EntityTypeBuilder<IdentityProfile> b) { b.ToTable("identity_profiles");
 b.HasKey(x => x.Id);
 b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
 b.Property(x => x.DisplayName).HasMaxLength(240);
 b.HasIndex(x => x.OrganizationId);
 } }
