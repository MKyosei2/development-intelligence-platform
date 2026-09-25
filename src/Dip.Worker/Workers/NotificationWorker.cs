// Purpose: Runs Notification background processing for DIP.
using Dip.Application.Abstractions.Messaging;
using Dip.Infrastructure.Messaging;
using Dip.Worker.Options;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class NotificationWorker : BackgroundService
{
    private readonly IQueueClient _queue;
    private readonly QueueOptions _options;
    private readonly WorkerOptions _workerOptions;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        IQueueClient queue,
        IOptions<QueueOptions> options,
        IOptions<WorkerOptions> workerOptions,
        ILogger<NotificationWorker> logger)
    {
        _queue = queue;
        _options = options.Value;
        _workerOptions = workerOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var consumer =
            $"{Environment.MachineName}-notification";

        var consumerGroup =
            $"{_options.ConsumerGroup}:notification";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await _queue.ReadAsync(
                    _options.StreamName,
                    consumerGroup,
                    consumer,
                    TimeSpan.FromSeconds(5),
                    stoppingToken);

                if (message is null)
                    continue;

                if (!message.MessageType.Contains(
                        "NotificationRequested",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await _queue.AcknowledgeAsync(
                        _options.StreamName,
                        consumerGroup,
                        message.MessageId,
                        stoppingToken);

                    continue;
                }

                try
                {
                    _logger.LogInformation(
                        "Processing notification {MessageId}",
                        message.MessageId);

                    await _queue.AcknowledgeAsync(
                        _options.StreamName,
                        consumerGroup,
                        message.MessageId,
                        stoppingToken);
                }
                catch (Exception exception)
                    when (exception is not OperationCanceledException)
                {
                    await HandleFailureAsync(
                        consumerGroup,
                        message,
                        exception,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Notification worker loop failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    stoppingToken);
            }
        }
    }

    private async Task HandleFailureAsync(
        string consumerGroup,
        QueueEnvelope message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (message.Attempt + 1
            >= Math.Max(1, _workerOptions.MaxAttempts))
        {
            await _queue.MoveToDeadLetterAsync(
                _options.StreamName,
                consumerGroup,
                _options.DeadLetterStreamName,
                message,
                exception.Message,
                cancellationToken);
        }
        else
        {
            await _queue.RetryAsync(
                _options.StreamName,
                consumerGroup,
                message,
                cancellationToken);
        }

        _logger.LogError(
            exception,
            "Notification failed {MessageId}",
            message.MessageId);
    }
}
