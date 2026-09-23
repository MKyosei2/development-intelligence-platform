// Purpose: Configures EF Core persistence for external work item links.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ExternalWorkItemLinkConfiguration : IEntityTypeConfiguration<ExternalWorkItemLink>
{
    public void Configure(EntityTypeBuilder<ExternalWorkItemLink> b)
    {
        b.ToTable("external_work_item_links");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.IncidentId).HasColumnName("incident_id");
        b.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(200);
        b.Property(x => x.Url).HasColumnName("url").HasMaxLength(2000);
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.IncidentId, x.Provider }).IsUnique();
    }
}
