using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using StackExchange.Redis;

namespace Dip.Infrastructure.Messaging;

public sealed class RedisQueueClient : IQueueClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentNullException.ThrowIfNull(message);

        var payload = JsonSerializer.Serialize(
            message,
            JsonOptions);

        var messageType =
            ReadTopLevelScalar(payload, "messageType")
            ?? typeof(T).FullName
            ?? typeof(T).Name;

        var correlationId =
            ReadTopLevelScalar(payload, "correlationId")
            ?? string.Empty;

        var attemptText =
            ReadTopLevelScalar(payload, "attempt")
            ?? "0";

        _ = int.TryParse(attemptText, out var attempt);

        await AddEnvelopeAsync(
            stream,
            messageType,
            payload,
            correlationId,
            Math.Max(0, attempt),
            cancellationToken);
    }

    public async Task<QueueEnvelope?> ReadAsync(
        string stream,
        string consumerGroup,
        string consumerName,
        TimeSpan block,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        if (block <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(block));

        var database = _redis.GetDatabase();

        try
        {
            await database.StreamCreateConsumerGroupAsync(
                stream,
                consumerGroup,
                "0-0",
                createStream: true);
        }
        catch (RedisServerException exception)
            when (exception.Message.Contains(
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
                    item => item.Name.ToString(),
                    item => item.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

                _ = int.TryParse(
                    values.GetValueOrDefault("attempt"),
                    out var attempt);

                return new QueueEnvelope(
                    entry.Id.ToString(),
                    values.GetValueOrDefault("messageType")
                        ?? "unknown",
                    values.GetValueOrDefault("payload")
                        ?? "{}",
                    Math.Max(0, attempt),
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
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        return _redis.GetDatabase().StreamAcknowledgeAsync(
            stream,
            consumerGroup,
            messageId);
    }

    public async Task RetryAsync(
        string stream,
        string consumerGroup,
        QueueEnvelope message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(message);

        // Add the replacement first. If Redis fails before the acknowledge,
        // the original remains pending instead of being silently lost.
        await AddEnvelopeAsync(
            stream,
            message.MessageType,
            message.PayloadJson,
            message.CorrelationId,
            checked(message.Attempt + 1),
            cancellationToken);

        await AcknowledgeAsync(
            stream,
            consumerGroup,
            message.MessageId,
            cancellationToken);
    }

    public async Task MoveToDeadLetterAsync(
        string sourceStream,
        string consumerGroup,
        string deadLetterStream,
        QueueEnvelope message,
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
        ArgumentException.ThrowIfNullOrWhiteSpace(deadLetterStream);
        ArgumentNullException.ThrowIfNull(message);

        var safeReason = string.IsNullOrWhiteSpace(reason)
            ? "Unknown queue processing failure."
            : reason.Trim();

        if (safeReason.Length > 4000)
            safeReason = safeReason[..4000];

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
                new("reason", safeReason),
                new(
                    "failedAtUtc",
                    DateTimeOffset.UtcNow.ToString("O"))
            });

        // Acknowledge only for this consumer group. Do not delete the stream
        // entry because another consumer group may still need to process it.
        await database.StreamAcknowledgeAsync(
            sourceStream,
            consumerGroup,
            message.MessageId);
    }

    private async Task AddEnvelopeAsync(
        string stream,
        string messageType,
        string payload,
        string correlationId,
        int attempt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

    private static string? ReadTopLevelScalar(
        string json,
        string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            foreach (var property in
                     document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals(
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value.ValueKind switch
                {
                    JsonValueKind.String =>
                        property.Value.GetString(),
                    JsonValueKind.Number =>
                        property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
