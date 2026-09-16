// Purpose: Implements P ro ce ss ed We bh oo kC on fi gu ra ti on persistence behavior.
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class ProcessedWebhookConfiguration : IEntityTypeConfiguration<ProcessedWebhook>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhook> builder)
    {
        builder.ToTable("ProcessedWebhooks");
        builder.HasKey(x => x.DeliveryId);
        builder.Property(x => x.DeliveryId).HasMaxLength(200);
        builder.Property(x => x.EventName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.HasIndex(x => x.ReceivedAtUtc);
    }
}
