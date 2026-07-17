using Dip.Application.Abstractions.Messaging;
using Dip.Infrastructure.Messaging;
using Dip.Worker.Options;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class NotificationWorker : BackgroundService
{
    private readonly IQueueClient _queue;
    private readonly RedisQueueOptions _options;
    private readonly WorkerOptions _workerOptions;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        IQueueClient queue,
        IOptions<RedisQueueOptions> options,
        IOptions<WorkerOptions> workerOptions,
        ILogger<NotificationWorker> logger)
    {
        _queue = queue;
        _options = options.Value;
        _workerOptions = workerOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = $"{Environment.MachineName}-notification";

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await _queue.ReadAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                consumer,
                TimeSpan.FromSeconds(5),
                stoppingToken);

            if (message is null)
            {
                await Task.Delay(500, stoppingToken);
                continue;
            }

            try
            {
                if (!message.MessageType.Contains(
                    "NotificationRequested",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _logger.LogInformation(
                    "Processing notification {MessageId}",
                    message.MessageId);

                await _queue.AcknowledgeAsync(
                    _options.StreamName,
                    _options.ConsumerGroup,
                    message.MessageId,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                if (message.Attempt + 1 >= _workerOptions.MaxAttempts)
                {
                    await _queue.MoveToDeadLetterAsync(
                        _options.StreamName,
                        _options.DeadLetterStream,
                        message,
                        ex.Message,
                        stoppingToken);
                }

                _logger.LogError(
                    ex,
                    "Notification failed {MessageId}",
                    message.MessageId);
            }
        }
    }
}
