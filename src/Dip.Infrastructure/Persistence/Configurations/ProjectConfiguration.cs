using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        // Keep this mapping identical to the formal legacy baseline. This
        // configuration and ProjectCoreConfiguration are both discovered by
        // ApplyConfigurationsFromAssembly, so conflicting names here would
        // produce a model/schema mismatch at runtime.
        builder.ToTable("Projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.RepositoryUrl).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DefaultBranch).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HealthScore)
            .HasConversion(score => score.Value,value => new HealthScore(value))
            .HasColumnName("HealthScore").HasPrecision(5, 2).IsRequired();
        builder.HasIndex(x => x.RepositoryUrl);
    }
}
