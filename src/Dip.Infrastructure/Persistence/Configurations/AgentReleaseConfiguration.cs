// Purpose: Configures persistence mapping for AgentRelease.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class AgentReleaseConfiguration : IEntityTypeConfiguration<AgentRelease> { public void Configure(EntityTypeBuilder<AgentRelease> b) { b.ToTable("agent_releases");
 b.HasKey(x => x.Id);
 b.Property(x => x.Version).HasMaxLength(80);
 b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(40);
 b.Property(x => x.State).HasConversion<string>().HasMaxLength(40);
 b.Property(x => x.PackageUri).HasMaxLength(1000);
 b.Property(x => x.Sha256).HasMaxLength(128);
 b.Property(x => x.Signature).HasMaxLength(8000);
 b.Property(x => x.MinimumServerVersion).HasMaxLength(80);
 b.HasIndex(x => new { x.OrganizationId, x.Channel, x.CreatedAtUtc });
 } }
