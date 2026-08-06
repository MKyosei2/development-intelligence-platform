using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration :
    IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(
        EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.MessageType)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(message => message.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.CorrelationId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.CausationId)
            .HasMaxLength(200);

        builder.Property(message => message.LastError)
            .HasMaxLength(4000);

        // This prevents two worker instances that selected the same pending
        // row from both acquiring it and publishing it.
        builder.Property(message => message.AttemptCount)
            .IsConcurrencyToken();

        builder.HasIndex(message => new
        {
            message.ProcessedAtUtc,
            message.NextAttemptAtUtc,
            message.LockedUntilUtc,
            message.CreatedAtUtc
        });

        builder.HasIndex(message => message.CorrelationId);
    }
}
