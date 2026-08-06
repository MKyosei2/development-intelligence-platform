using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProcessedWebhookConfiguration :
    IEntityTypeConfiguration<ProcessedWebhook>
{
    public void Configure(
        EntityTypeBuilder<ProcessedWebhook> builder)
    {
        builder.ToTable("processed_webhooks");

        // ProcessedWebhook does not have a separate Id property.
        // GitHub delivery IDs are unique and are the natural primary key.
        builder.HasKey(x => x.DeliveryId);

        builder.Property(x => x.DeliveryId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.EventName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PayloadHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(4000);

        builder.HasIndex(x => x.ProcessedAtUtc);
        builder.HasIndex(x => x.ReceivedAtUtc);
    }
}
