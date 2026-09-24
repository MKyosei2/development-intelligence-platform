// Purpose: Implements I Queue Client responsibilities for messaging.
namespace Dip.Application.Abstractions.Messaging;

public sealed record QueueEnvelope(
    string MessageId,
    string MessageType,
    string PayloadJson,
    int Attempt,
    string CorrelationId);

public interface IQueueClient
{
    Task PublishAsync<T>(string stream, T message, CancellationToken cancellationToken);
    Task<QueueEnvelope?> ReadAsync(string stream, string consumerGroup, string consumerName, TimeSpan block, CancellationToken cancellationToken);
    Task AcknowledgeAsync(string stream, string consumerGroup, string messageId, CancellationToken cancellationToken);
    Task RetryAsync(string stream, string consumerGroup, QueueEnvelope message, CancellationToken cancellationToken);
    Task MoveToDeadLetterAsync(string sourceStream, string consumerGroup, string deadLetterStream, QueueEnvelope message, string reason, CancellationToken cancellationToken);
}
