using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Infrastructure.Messaging;
using Dip.Worker.Options;
using Microsoft.EntityFrameworkCore;
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
                    Math.Max(
                        1,
                        _workerOptions.PollingIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task PublishBatchAsync(
        CancellationToken cancellationToken)
    {
        Guid[] messageIds;

        using (var scope = _scopeFactory.CreateScope())
        {
            var repository = scope.ServiceProvider
                .GetRequiredService<IOutboxRepository>();

            var pending = await repository.GetPendingAsync(
                Math.Clamp(
                    _workerOptions.BatchSize,
                    1,
                    500),
                DateTimeOffset.UtcNow,
                cancellationToken);

            messageIds = pending
                .Select(message => message.Id)
                .ToArray();
        }

        foreach (var messageId in messageIds)
        {
            await PublishOneAsync(
                messageId,
                cancellationToken);
        }
    }

    private async Task PublishOneAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IOutboxRepository>();

        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        var message = await repository.GetByIdAsync(
            messageId,
            cancellationToken);

        if (message is null || message.ProcessedAtUtc is not null)
            return;

        var now = DateTimeOffset.UtcNow;

        if (message.NextAttemptAtUtc is not null
            && message.NextAttemptAtUtc > now)
        {
            return;
        }

        if (message.LockedUntilUtc is not null
            && message.LockedUntilUtc > now)
        {
            return;
        }

        try
        {
            message.Lock(
                now.AddSeconds(
                    Math.Max(
                        10,
                        _workerOptions.LockSeconds)));

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogDebug(
                "Outbox message {MessageId} was acquired by another worker.",
                messageId);

            return;
        }

        try
        {
            using var payloadDocument =
                JsonDocument.Parse(message.PayloadJson);

            var envelope = new
            {
                messageId = message.Id.ToString("N"),
                messageType = message.MessageType,
                payloadVersion = "1.0",
                correlationId = message.CorrelationId,
                causationId = message.CausationId,

                // Queue delivery attempts are independent from attempts made
                // while publishing the database outbox row to Redis.
                attempt = 0,

                createdAtUtc = message.CreatedAtUtc,
                payload = payloadDocument.RootElement.Clone()
            };

            await _queue.PublishAsync(
                _queueOptions.StreamName,
                envelope,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var delay = TimeSpan.FromSeconds(
                Math.Min(
                    300,
                    Math.Pow(
                        2,
                        Math.Min(
                            message.AttemptCount + 1,
                            8))));

            var error = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;

            message.MarkFailed(
                error,
                DateTimeOffset.UtcNow.Add(delay));

            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                exception,
                "Failed to publish outbox message {MessageId}. Attempt={Attempt}; NextAttemptAtUtc={NextAttemptAtUtc}.",
                message.Id,
                message.AttemptCount,
                message.NextAttemptAtUtc);

            return;
        }

        message.MarkProcessed(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Published outbox message {MessageId} as {MessageType}.",
            message.Id,
            message.MessageType);
    }
}
