using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using StackExchange.Redis;

namespace Dip.Infrastructure.Messaging;

public sealed class RedisQueueClient : IQueueClient
{
    private readonly IConnectionMultiplexer _redis;

    public RedisQueueClient(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync<T>(
        string stream,
        T message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(message);
        var messageType = typeof(T).FullName ?? typeof(T).Name;
        var correlationId = TryReadProperty(payload, "correlationId");
        var attempt = TryReadProperty(payload, "attempt");

        await _redis.GetDatabase().StreamAddAsync(
            stream,
            new NameValueEntry[]
            {
                new("messageType", messageType),
                new("payload", payload),
                new("correlationId", correlationId),
                new("attempt", attempt)
            });
    }

    public async Task<QueueEnvelope?> ReadAsync(
        string stream,
        string consumerGroup,
        string consumerName,
        TimeSpan block,
        CancellationToken cancellationToken)
    {
        var database = _redis.GetDatabase();

        try
        {
            await database.StreamCreateConsumerGroupAsync(
                stream,
                consumerGroup,
                "0-0",
                createStream: true);
        }
        catch (RedisServerException ex)
            when (ex.Message.Contains(
                "BUSYGROUP",
                StringComparison.OrdinalIgnoreCase))
        {
        }

        var deadline = DateTimeOffset.UtcNow + block;

        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = await database.StreamReadGroupAsync(
                stream,
                consumerGroup,
                consumerName,
                ">",
                count: 1);

            if (entries.Length > 0)
            {
                var entry = entries[0];
                var values = entry.Values.ToDictionary(
                    x => x.Name.ToString(),
                    x => x.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

                _ = int.TryParse(
                    values.GetValueOrDefault("attempt"),
                    out var attempt);

                return new QueueEnvelope(
                    entry.Id!,
                    values.GetValueOrDefault("messageType") ?? "unknown",
                    values.GetValueOrDefault("payload") ?? "{}",
                    attempt,
                    values.GetValueOrDefault("correlationId")
                        ?? string.Empty);
            }

            if (DateTimeOffset.UtcNow >= deadline)
                return null;

            await Task.Delay(
                TimeSpan.FromMilliseconds(100),
                cancellationToken);
        }

        return null;
    }

    public Task AcknowledgeAsync(
        string stream,
        string consumerGroup,
        string messageId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _redis.GetDatabase().StreamAcknowledgeAsync(
            stream,
            consumerGroup,
            messageId);
    }

    public async Task MoveToDeadLetterAsync(
        string sourceStream,
        string deadLetterStream,
        QueueEnvelope message,
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _redis.GetDatabase();

        await database.StreamAddAsync(
            deadLetterStream,
            new NameValueEntry[]
            {
                new("sourceMessageId", message.MessageId),
                new("messageType", message.MessageType),
                new("payload", message.PayloadJson),
                new("attempt", message.Attempt),
                new("correlationId", message.CorrelationId),
                new("reason", reason),
                new("failedAtUtc", DateTimeOffset.UtcNow.ToString("O"))
            });

        await database.StreamAcknowledgeAsync(
            sourceStream,
            "dip-workers",
            message.MessageId);

        await database.StreamDeleteAsync(
            sourceStream,
            new RedisValue[] { message.MessageId });
    }

    private static string TryReadProperty(
        string json,
        string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(
                    propertyName,
                    out var property))
                return property.ToString();
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }
}
