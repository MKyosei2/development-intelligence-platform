using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Infrastructure.Messaging;
using Dip.Worker.Options;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQueueClient _queue;
    private readonly RedisQueueOptions _queueOptions;
    private readonly WorkerOptions _workerOptions;
    private readonly ILogger<OutboxWorker> _logger;

    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        IQueueClient queue,
        IOptions<RedisQueueOptions> queueOptions,
        IOptions<WorkerOptions> workerOptions,
        ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _queueOptions = queueOptions.Value;
        _workerOptions = workerOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
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
                    "Outbox polling cycle failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(
                    Math.Max(1, _workerOptions.PollingIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task PublishBatchAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        var now = DateTimeOffset.UtcNow;
        var messages = await repository.GetPendingAsync(
            Math.Clamp(_workerOptions.BatchSize, 1, 500),
            now,
            cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                message.Lock(
                    now.AddSeconds(
                        Math.Max(10, _workerOptions.LockSeconds)));
                await unitOfWork.SaveChangesAsync(cancellationToken);

                using var payloadDocument =
                    JsonDocument.Parse(message.PayloadJson);

                var envelope = new
                {
                    messageId = message.Id.ToString("N"),
                    messageType = message.MessageType,
                    payloadVersion = "1.0",
                    correlationId = message.CorrelationId,
                    causationId = message.CausationId,
                    attempt = message.AttemptCount,
                    createdAtUtc = message.CreatedAtUtc,
                    payload = payloadDocument.RootElement.Clone()
                };

                await _queue.PublishAsync(
                    _queueOptions.StreamName,
                    envelope,
                    cancellationToken);

                message.MarkProcessed(DateTimeOffset.UtcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Published outbox message {MessageId} as {MessageType}.",
                    message.Id,
                    message.MessageType);
            }
            catch (Exception exception)
            {
                var delay = TimeSpan.FromSeconds(
                    Math.Min(
                        300,
                        Math.Pow(
                            2,
                            Math.Min(message.AttemptCount + 1, 8))));

                message.MarkFailed(
                    exception.Message,
                    DateTimeOffset.UtcNow.Add(delay));

                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "Failed to publish outbox message {MessageId}. Attempt={Attempt}.",
                    message.Id,
                    message.AttemptCount);
            }
        }
    }
}
