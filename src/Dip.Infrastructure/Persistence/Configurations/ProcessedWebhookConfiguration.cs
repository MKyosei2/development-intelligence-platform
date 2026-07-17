using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProcessedWebhookConfiguration : IEntityTypeConfiguration<ProcessedWebhook>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhook> builder)
    {
        builder.ToTable("processed_webhooks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeliveryId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EventName).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.DeliveryId).IsUnique();
        builder.HasIndex(x => x.ProcessedAtUtc);
    }
}
