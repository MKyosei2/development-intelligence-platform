// Purpose: Coordinates O ut bo xW ri te r application behavior for Operations.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
namespace Dip.Application.Services;
public sealed class OutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IOutboxRepository _outbox;
    public OutboxWriter(IOutboxRepository outbox) => _outbox = outbox;
    public Task EnqueueAsync<T>(T payload,string correlationId,string? causationId,DateTimeOffset createdAtUtc,CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("CorrelationId is required.",nameof(correlationId));
        var message=new OutboxMessage(Guid.NewGuid(),typeof(T).FullName??typeof(T).Name,JsonSerializer.Serialize(payload,JsonOptions),correlationId.Trim(),string.IsNullOrWhiteSpace(causationId)?null:causationId.Trim(),createdAtUtc);
        return _outbox.AddAsync(message,cancellationToken);
    }
}
