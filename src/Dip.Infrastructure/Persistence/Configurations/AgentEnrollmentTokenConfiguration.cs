// Purpose: Configures persistence mapping for AgentEnrollmentToken.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class AgentEnrollmentTokenConfiguration:IEntityTypeConfiguration<AgentEnrollmentToken>{public void Configure(EntityTypeBuilder<AgentEnrollmentToken>b){b.ToTable("agent_enrollment_tokens");b.HasKey(x=>x.Id);b.Property(x=>x.TokenHash).HasMaxLength(128);b.HasIndex(x=>x.TokenHash).IsUnique();}}
