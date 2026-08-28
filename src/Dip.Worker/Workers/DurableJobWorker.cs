using System.Diagnostics;
using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Observability;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Jobs;
using Dip.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class DurableJobWorker(
    IServiceScopeFactory scopes,
    IQueueClient queue,
    IOptions<DurableJobQueueOptions> options,
    IDipOperationalMetrics metrics,
    ILogger<DurableJobWorker> logger) : BackgroundService
{
    private readonly DurableJobQueueOptions _o = options.Value;
    private readonly string _consumer =
        $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using (var depthScope = scopes.CreateScope())
                {
                    var depthRepo = depthScope.ServiceProvider
                        .GetRequiredService<IDurableJobRepository>();
                    metrics.QueueDepthObserved(await depthRepo.CountQueuedAsync(stop));
                }

                var envelope = await queue.ReadAsync(
                    _o.StreamName,
                    _o.ConsumerGroup,
                    _consumer,
                    TimeSpan.FromMilliseconds(Math.Max(100, _o.BlockMilliseconds)),
                    stop);
                if (envelope is null)
                    continue;

                try
                {
                    await ProcessAsync(envelope, stop);
                    await queue.AcknowledgeAsync(
                        _o.StreamName,
                        _o.ConsumerGroup,
                        envelope.MessageId,
                        stop);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Durable job message {MessageId} failed.",
                        envelope.MessageId);
                    await queue.MoveToDeadLetterAsync(
                        _o.StreamName,
                        _o.ConsumerGroup,
                        _o.DeadLetterStreamName,
                        envelope,
                        ex.Message,
                        stop);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Durable job worker loop failed.");
                await Task.Delay(1000, stop);
            }
        }
    }

    private async Task ProcessAsync(QueueEnvelope envelope, CancellationToken stop)
    {
        var jobId = ReadJobId(envelope.PayloadJson);
        using var scope = scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDurableJobRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var lease = TimeSpan.FromSeconds(Math.Clamp(_o.LeaseSeconds, 15, 3600));
        var job = await repo.TryClaimAsync(
            jobId,
            _consumer,
            DateTimeOffset.UtcNow,
            lease,
            stop);
        if (job is null)
            return;

        var handler = scope.ServiceProvider
            .GetServices<IDurableJobHandler>()
            .SingleOrDefault(candidate => candidate.Type == job.Type)
            ?? throw new InvalidOperationException(
                $"No durable job handler is registered for {job.Type}.");

        metrics.JobStarted(job.Type.ToString());
        var executionStopwatch = Stopwatch.StartNew();

        using var requestedCancellation = new CancellationTokenSource();
        using var leaseLostCancellation = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stop,
            requestedCancellation.Token,
            leaseLostCancellation.Token);
        var monitor = MonitorAsync(
            job.Id,
            requestedCancellation,
            leaseLostCancellation,
            stop);

        try
        {
            var executionTask = handler.ExecuteAsync(job, linked.Token);
            var completed = await Task.WhenAny(executionTask, monitor);

            if (completed == monitor && monitor.IsFaulted)
            {
                leaseLostCancellation.Cancel();
                await monitor;
            }

            var result = await executionTask;

            if (!await OwnsLeaseAsync(job.Id, stop))
            {
                leaseLostCancellation.Cancel();
                logger.LogWarning(
                    "Durable job {JobId} completed handler work after losing lease ownership. " +
                    "The stale worker result was discarded.",
                    job.Id);
                return;
            }

            job.Complete(result.ResultJson, DateTimeOffset.UtcNow);
            await uow.SaveChangesAsync(stop);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (leaseLostCancellation.IsCancellationRequested)
        {
            logger.LogWarning(
                "Durable job {JobId} stopped because lease ownership was lost. " +
                "The current owner is responsible for final state.",
                job.Id);
        }
        catch (OperationCanceledException) when (requestedCancellation.IsCancellationRequested)
        {
            if (await OwnsLeaseAsync(job.Id, stop))
            {
                job.Cancel(DateTimeOffset.UtcNow);
                await uow.SaveChangesAsync(stop);
            }
        }
        catch (TimeoutException ex)
        {
            if (await OwnsLeaseAsync(job.Id, stop))
            {
                job.Fail("timeout", ex.Message, DateTimeOffset.UtcNow, true);
                await uow.SaveChangesAsync(stop);
            }
            else
            {
                logger.LogWarning(
                    ex,
                    "Durable job {JobId} timed out after lease ownership changed; " +
                    "stale failure state was not persisted.",
                    job.Id);
            }
        }
        catch (Exception ex)
        {
            if (await OwnsLeaseAsync(job.Id, stop))
            {
                job.Fail("handler-failure", ex.Message, DateTimeOffset.UtcNow);
                await uow.SaveChangesAsync(stop);
            }

            logger.LogError(
                ex,
                "Durable job {JobId} failed in handler {JobType}.",
                job.Id,
                job.Type);
        }
        finally
        {
            executionStopwatch.Stop();
            metrics.JobFinished(
                job.Type.ToString(),
                job.Status.ToString(),
                executionStopwatch.Elapsed);

            requestedCancellation.Cancel();
            leaseLostCancellation.Cancel();
            try
            {
                await monitor;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Job heartbeat monitor ended with an error for {JobId}.",
                    job.Id);
            }
        }
    }

    private async Task MonitorAsync(
        Guid jobId,
        CancellationTokenSource requestedCancellation,
        CancellationTokenSource leaseLostCancellation,
        CancellationToken stop)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(_o.HeartbeatSeconds, 2, 300));
        var lease = TimeSpan.FromSeconds(Math.Clamp(_o.LeaseSeconds, 15, 3600));
        using var monitorStop = CancellationTokenSource.CreateLinkedTokenSource(
            stop,
            requestedCancellation.Token,
            leaseLostCancellation.Token);
        var ct = monitorStop.Token;

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(delay, ct);

            try
            {
                using var scope = scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IDurableJobRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var current = await repo.GetAsync(jobId, ct);

                if (current is null)
                {
                    logger.LogError(
                        "Durable job {JobId} disappeared while running; cancelling stale execution.",
                        jobId);
                    leaseLostCancellation.Cancel();
                    return;
                }

                if (current.CancellationRequestedAtUtc is not null)
                {
                    requestedCancellation.Cancel();
                    return;
                }

                if (current.Status != DurableJobStatus.Running
                    || !string.Equals(current.LeaseOwner, _consumer, StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "Durable job {JobId} lease ownership changed from {Consumer} to {Owner} " +
                        "with status {Status}; cancelling stale execution.",
                        jobId,
                        _consumer,
                        current.LeaseOwner ?? "none",
                        current.Status);
                    leaseLostCancellation.Cancel();
                    return;
                }

                current.Heartbeat(
                    _consumer,
                    DateTimeOffset.UtcNow,
                    lease);
                await uow.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Durable job {JobId} heartbeat failed; cancelling execution to avoid split-brain work.",
                    jobId);
                leaseLostCancellation.Cancel();
                throw;
            }
        }
    }

    private async Task<bool> OwnsLeaseAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDurableJobRepository>();
        var current = await repo.GetAsync(jobId, cancellationToken);
        return current is not null
            && current.Status == DurableJobStatus.Running
            && string.Equals(current.LeaseOwner, _consumer, StringComparison.Ordinal)
            && current.LeaseExpiresAtUtc is { } expiresAt
            && expiresAt > DateTimeOffset.UtcNow;
    }

    private static Guid ReadJobId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.TryGetProperty("payload", out var payload)
            ? payload
            : doc.RootElement;

        if (root.ValueKind == JsonValueKind.String)
        {
            using var nested = JsonDocument.Parse(root.GetString()!);
            root = nested.RootElement.Clone();
        }

        if (root.TryGetProperty("jobId", out var id)
            && Guid.TryParse(id.GetString(), out var parsed))
        {
            return parsed;
        }

        throw new InvalidDataException(
            "Durable job queue payload does not contain jobId.");
    }
}
