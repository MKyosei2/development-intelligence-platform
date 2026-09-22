// Purpose: Configures persistence mapping for ProjectTenantBinding.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProjectTenantBindingConfiguration : IEntityTypeConfiguration<ProjectTenantBinding> { public void Configure(EntityTypeBuilder<ProjectTenantBinding> b) { b.ToTable("project_tenant_bindings");
 b.HasKey(x => x.Id);
 b.HasIndex(x => x.ProjectId).IsUnique();
 b.HasIndex(x => new { x.OrganizationId, x.ProjectId });
 b.Property(x => x.PortfolioKey).HasMaxLength(120);
 } }
