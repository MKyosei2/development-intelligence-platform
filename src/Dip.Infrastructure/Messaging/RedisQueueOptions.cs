namespace Dip.Infrastructure.Messaging;

public sealed class RedisQueueOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = "localhost:6379";
    public string StreamName { get; init; } = "dip-events";
    public string ConsumerGroup { get; init; } = "dip-workers";
    public string DeadLetterStream { get; init; } = "dip-events-dlq";
}
