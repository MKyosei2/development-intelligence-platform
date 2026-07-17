using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dip.Infrastructure.Messaging;

public sealed class RedisQueueClient : IQueueClient
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisQueueOptions _options;

    public RedisQueueClient(
        IConnectionMultiplexer redis,
        IOptions<RedisQueueOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    public async Task PublishAsync<T>(
        string stream,
        T message,
        CancellationToken cancellationToken)
    {
        var database = _redis.GetDatabase();
        var payload = JsonSerializer.Serialize(message);
        await database.StreamAddAsync(
            stream,
            new[]
            {
                new NameValueEntry("messageType", typeof(T).FullName ?? typeof(T).Name),
                new NameValueEntry("payload", payload)
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
                true);
        }
        catch (RedisServerException ex) when (
            ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
        }

        var entries = await database.StreamReadGroupAsync(
            stream,
            consumerGroup,
            consumerName,
            ">",
            count: 1);

        if (entries.Length == 0)
            return null;

        var entry = entries[0];
        var values = entry.Values.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        return new QueueEnvelope(
            entry.Id!,
            values.GetValueOrDefault("messageType") ?? "unknown",
            values.GetValueOrDefault("payload") ?? "{}",
            0,
            values.GetValueOrDefault("correlationId") ?? string.Empty);
    }

    public Task AcknowledgeAsync(
        string stream,
        string consumerGroup,
        string messageId,
        CancellationToken cancellationToken)
    {
        return _redis.GetDatabase()
            .StreamAcknowledgeAsync(stream, consumerGroup, messageId);
    }

    public async Task MoveToDeadLetterAsync(
        string sourceStream,
        string deadLetterStream,
        QueueEnvelope message,
        string reason,
        CancellationToken cancellationToken)
    {
        var database = _redis.GetDatabase();

        await database.StreamAddAsync(
            deadLetterStream,
            new[]
            {
                new NameValueEntry("sourceMessageId", message.MessageId),
                new NameValueEntry("messageType", message.MessageType),
                new NameValueEntry("payload", message.PayloadJson),
                new NameValueEntry("attempt", message.Attempt),
                new NameValueEntry("correlationId", message.CorrelationId),
                new NameValueEntry("reason", reason),
                new NameValueEntry("failedAtUtc", DateTimeOffset.UtcNow.ToString("O"))
            });

        await database.StreamDeleteAsync(sourceStream, new RedisValue[] { message.MessageId });
    }
}
