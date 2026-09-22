// Purpose: Configures persistence mapping for OrganizationRoleBinding.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class OrganizationRoleBindingConfiguration : IEntityTypeConfiguration<OrganizationRoleBinding> { public void Configure(EntityTypeBuilder<OrganizationRoleBinding> b) { b.ToTable("organization_role_bindings");
 b.HasKey(x => x.Id);
 b.Property(x => x.SubjectId).HasMaxLength(300);
 b.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
 b.Property(x => x.GrantedBy).HasMaxLength(240);
 b.HasIndex(x => new { x.OrganizationId, x.SubjectId, x.ProjectId });
 } }
