// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class IntegrationConnectionConfiguration
    : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.ToTable("integration_connections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(1000);
        builder.Property(x => x.CredentialReference).HasColumnName("credential_reference").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(x => x.ExternalAccount).HasColumnName("external_account").HasMaxLength(300);
        builder.Property(x => x.HealthState).HasColumnName("health_state").HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(x => x.LastHealthMessage).HasColumnName("last_health_message").HasMaxLength(2000);
        builder.Property(x => x.LastTestedAtUtc).HasColumnName("last_tested_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(x => new { x.Provider, x.Enabled });
        builder.HasIndex(x => x.DisplayName);
    }
}

public sealed class ProjectSourceBindingConfiguration
    : IEntityTypeConfiguration<ProjectSourceBinding>
{
    public void Configure(EntityTypeBuilder<ProjectSourceBinding> builder)
    {
        builder.ToTable("project_source_bindings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.ConnectionId).HasColumnName("connection_id").IsRequired();
        builder.Property(x => x.ExternalResourceId).HasColumnName("external_resource_id").HasMaxLength(300).IsRequired();
        builder.Property(x => x.ExternalResourceName).HasColumnName("external_resource_name").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConfigurationJson).HasColumnName("configuration_json").HasMaxLength(16000);
        builder.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(x => x.LastSyncedAtUtc).HasColumnName("last_synced_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProjectId, x.ConnectionId, x.ExternalResourceId })
            .IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Enabled });
    }
}

public sealed class PolicyProfileConfiguration
    : IEntityTypeConfiguration<PolicyProfile>
{
    public void Configure(EntityTypeBuilder<PolicyProfile> builder)
    {
        builder.ToTable("policy_profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ScopeType).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(300);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(x => x.EvidencePolicyJson).HasColumnName("evidence_policy_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.AgentPolicyJson).HasColumnName("agent_policy_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.RetentionPolicyJson).HasColumnName("retention_policy_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.AlertPolicyJson).HasColumnName("alert_policy_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.PrivacyPolicyJson).HasColumnName("privacy_policy_json").HasMaxLength(32000).IsRequired();
        builder.Property(x => x.QaPolicyId).HasColumnName("qa_policy_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<QaPolicy>()
            .WithMany()
            .HasForeignKey(x => x.QaPolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ScopeType);
        builder.HasIndex(x => x.ProjectId)
            .IsUnique()
            .HasFilter("\"project_id\" IS NOT NULL AND \"scope_type\" = 'Project'");
        builder.HasIndex(x => x.AgentId)
            .IsUnique()
            .HasFilter("\"agent_id\" IS NOT NULL AND \"scope_type\" = 'Agent'");
        builder.HasIndex(x => x.ScopeType)
            .IsUnique()
            .HasFilter("\"scope_type\" = 'Global'");
    }
}
