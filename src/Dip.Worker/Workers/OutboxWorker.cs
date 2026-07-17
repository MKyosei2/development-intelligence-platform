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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider
                .GetRequiredService<IOutboxRepository>();
            var unitOfWork = scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

            var now = DateTimeOffset.UtcNow;
            var messages = await repository.GetPendingAsync(
                _workerOptions.BatchSize,
                now,
                stoppingToken);

            foreach (var message in messages)
            {
                try
                {
                    message.Lock(now.AddSeconds(_workerOptions.LockSeconds));
                    await unitOfWork.SaveChangesAsync(stoppingToken);

                    var envelope = new
                    {
                        messageId = message.Id.ToString("N"),
                        messageType = message.MessageType,
                        payloadVersion = "1.0",
                        correlationId = message.CorrelationId,
                        causationId = message.CausationId,
                        attempt = message.AttemptCount,
                        createdAtUtc = message.CreatedAtUtc,
                        payload = JsonDocument.Parse(message.PayloadJson).RootElement
                    };

                    await _queue.PublishAsync(
                        _queueOptions.StreamName,
                        envelope,
                        stoppingToken);

                    message.MarkProcessed(DateTimeOffset.UtcNow);
                    await unitOfWork.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    var delay = TimeSpan.FromSeconds(
                        Math.Min(300, Math.Pow(2, message.AttemptCount + 1)));

                    message.MarkFailed(
                        ex.Message,
                        DateTimeOffset.UtcNow.Add(delay));

                    await unitOfWork.SaveChangesAsync(stoppingToken);

                    _logger.LogError(
                        ex,
                        "Failed to publish outbox message {MessageId}",
                        message.Id);
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_workerOptions.PollingIntervalSeconds),
                stoppingToken);
        }
    }
}
