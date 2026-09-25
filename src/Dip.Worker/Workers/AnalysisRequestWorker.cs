// Purpose: Runs Analysis Request background processing for DIP.
using Dip.Application.Abstractions.Messaging;
using Dip.Infrastructure.Messaging;
using Dip.Worker.Options;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class AnalysisRequestWorker : BackgroundService
{
    private readonly IQueueClient _queue;
    private readonly QueueOptions _options;
    private readonly WorkerOptions _workerOptions;
    private readonly ILogger<AnalysisRequestWorker> _logger;

    public AnalysisRequestWorker(
        IQueueClient queue,
        IOptions<QueueOptions> options,
        IOptions<WorkerOptions> workerOptions,
        ILogger<AnalysisRequestWorker> logger)
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
            $"{Environment.MachineName}-analysis";

        var consumerGroup =
            $"{_options.ConsumerGroup}:analysis";

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
                        "AnalysisRequested",
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
                        "Processing analysis request {MessageId}",
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
                    "Analysis worker loop failed.");

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
            "Analysis request failed {MessageId}",
            message.MessageId);
    }
}
