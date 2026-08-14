using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectKey).HasColumnName("subject_key").HasMaxLength(200);
        builder.Property(x => x.DetailJson).HasColumnName("detail_json").HasMaxLength(16000);
        builder.Property(x => x.RequestId).HasColumnName("request_id").HasMaxLength(200);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(200);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}
