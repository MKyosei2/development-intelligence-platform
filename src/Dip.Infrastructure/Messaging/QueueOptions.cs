// Purpose: Implements Queue Options infrastructure behavior for messaging.
namespace Dip.Infrastructure.Messaging;
public sealed class QueueOptions
{
    public const string SectionName = "Queue";
    public string StreamName { get; set; } = "dip:events";
    public string DeadLetterStreamName { get; set; } = "dip:events:dead-letter";
    public string ConsumerGroup { get; set; } = "dip-workers";
    public int MaxDeliveryCount { get; set; } = 5;
    public int ReadCount { get; set; } = 1;
    public int BlockMilliseconds { get; set; } = 5000;
}
