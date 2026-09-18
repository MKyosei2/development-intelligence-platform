// Purpose: Configures EF Core persistence for P ro je ct Cl ai mC on fi gu ra ti on.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProjectClaimConfiguration : IEntityTypeConfiguration<ProjectClaim>
{
    public void Configure(EntityTypeBuilder<ProjectClaim> builder)
    {
        builder.ToTable("project_claims");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClaimedStatus).HasColumnName("claimed_status").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ClaimedProgress).HasColumnName("claimed_progress").HasPrecision(5, 2);
        builder.Property(x => x.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(x => x.ClaimedAtUtc).HasColumnName("claimed_at_utc").IsRequired();
        builder.Property(x => x.VerificationStatus).HasColumnName("verification_status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.LastVerifiedAtUtc).HasColumnName("last_verified_at_utc");

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.SubjectKey, x.ClaimedAtUtc });
    }
}
