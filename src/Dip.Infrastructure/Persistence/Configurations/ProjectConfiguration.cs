using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration :
    IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.RepositoryUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.DefaultBranch)
            .HasMaxLength(200)
            .IsRequired();

        // HealthScore is a value object, not an owned entity.
        builder.Property(x => x.HealthScore)
            .HasConversion(
                score => score.Value,
                value => new HealthScore(value))
            .HasColumnName("health_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.HasIndex(x => x.RepositoryUrl)
            .IsUnique();
    }
}
