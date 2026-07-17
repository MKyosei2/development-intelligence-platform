using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MessageType).HasMaxLength(500).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CausationId).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(4000);

        builder.HasIndex(x => new
        {
            x.ProcessedAtUtc,
            x.NextAttemptAtUtc,
            x.LockedUntilUtc,
            x.CreatedAtUtc
        });

        builder.HasIndex(x => x.CorrelationId);
    }
}
