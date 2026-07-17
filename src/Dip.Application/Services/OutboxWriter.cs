using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;

namespace Dip.Application.Services;

public sealed class OutboxWriter
{
    private readonly IOutboxRepository _outbox;

    public OutboxWriter(IOutboxRepository outbox)
    {
        _outbox = outbox;
    }

    public Task EnqueueAsync<T>(
        T payload,
        string correlationId,
        string? causationId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var message = new OutboxMessage(
            Guid.NewGuid(),
            typeof(T).FullName ?? typeof(T).Name,
            JsonSerializer.Serialize(payload),
            correlationId,
            causationId,
            createdAtUtc);

        return _outbox.AddAsync(message, cancellationToken);
    }
}
