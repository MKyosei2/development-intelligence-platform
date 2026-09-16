// Purpose: Configures persistence mapping for AgentRegistration.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class AgentRegistrationConfiguration:IEntityTypeConfiguration<AgentRegistration>{public void Configure(EntityTypeBuilder<AgentRegistration>b){b.ToTable("agent_registrations");b.HasKey(x=>x.Id);b.Property(x=>x.AgentId).HasMaxLength(160);b.Property(x=>x.MachineId).HasMaxLength(160);b.Property(x=>x.PublicKeyPem).HasMaxLength(12000);b.Property(x=>x.Version).HasMaxLength(80);b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(40);b.Property(x=>x.HealthJson).HasMaxLength(32000);b.HasIndex(x=>x.AgentId).IsUnique();b.HasIndex(x=>new{x.OrganizationId,x.ProjectId});}}
