using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

// Purpose: Configures EF Core persistence for performance budget profiles.
public sealed class PerformanceBudgetProfileConfiguration : IEntityTypeConfiguration<PerformanceBudgetProfile>
{
    public void Configure(EntityTypeBuilder<PerformanceBudgetProfile> b)
    {
        b.ToTable("performance_budget_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Key).HasColumnName("key").HasMaxLength(120);
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        b.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(80);
        b.Property(x => x.TargetProfile).HasColumnName("target_profile").HasMaxLength(120);
        b.Property(x => x.RulesJson).HasColumnName("rules_json").HasMaxLength(32000);
        b.Property(x => x.Enabled).HasColumnName("enabled");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
        b.HasIndex(x => x.Key).IsUnique().HasFilter("project_id IS NULL");
    }
}
