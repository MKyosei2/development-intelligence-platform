// Purpose: Configures persistence mapping for OrganizationTenant.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class OrganizationTenantConfiguration : IEntityTypeConfiguration<OrganizationTenant> { public void Configure(EntityTypeBuilder<OrganizationTenant> b) { b.ToTable("organizations");
 b.HasKey(x => x.Id);
 b.Property(x => x.Name).HasMaxLength(200);
 b.Property(x => x.Slug).HasMaxLength(100);
 b.HasIndex(x => x.Slug).IsUnique();
 } }
