namespace Dip.Infrastructure.Messaging;
public sealed class RedisQueueOptions
{
    public const string SectionName = "RedisQueue";
    public string StreamName { get; set; } = "dip:events";
    public string DeadLetterStreamName { get; set; } = "dip:events:dead-letter";
    public string ConsumerGroup { get; set; } = "dip-workers";
    public int MaxDeliveryCount { get; set; } = 5;
    public int ReadCount { get; set; } = 1;
    public int BlockMilliseconds { get; set; } = 5000;
}
