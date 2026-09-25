// Purpose: Runs Durable Job background processing for DIP.
using System.Diagnostics;
using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Observability;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Jobs;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        var finalMetricStatus = DurableJobStatus.Running.ToString();

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
                // Monitor already logged the root cause. Cancellation prevents the
                // handler from continuing side effects after lease ownership is uncertain.
                leaseLostCancellation.Cancel();
            }

            var result = await executionTask;
            if (await TryCompleteOwnedLeaseAsync(job.Id, result.ResultJson, stop))
            {
                finalMetricStatus = DurableJobStatus.Succeeded.ToString();
            }
            else if (await TryCancelRequestedOwnedLeaseAsync(job.Id, stop))
            {
                finalMetricStatus = DurableJobStatus.Cancelled.ToString();
            }
            else
            {
                finalMetricStatus = "StaleDiscarded";
                leaseLostCancellation.Cancel();
                logger.LogWarning(
                    "Durable job {JobId} completed handler work after losing a valid lease. " +
                    "The result was discarded by an atomic owner/expiry guard.",
                    job.Id);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (requestedCancellation.IsCancellationRequested)
        {
            if (await TryCancelRequestedOwnedLeaseAsync(job.Id, stop))
                finalMetricStatus = DurableJobStatus.Cancelled.ToString();
            else
                finalMetricStatus = "StaleDiscarded";
        }
        catch (OperationCanceledException) when (leaseLostCancellation.IsCancellationRequested)
        {
            finalMetricStatus = "LeaseLost";
            logger.LogWarning(
                "Durable job {JobId} stopped because lease ownership was lost. " +
                "No stale terminal state was persisted.",
                job.Id);
        }
        catch (TimeoutException ex)
        {
            if (await TryFailOwnedLeaseAsync(job.Id, "timeout", ex.Message, true, stop))
            {
                finalMetricStatus = DurableJobStatus.TimedOut.ToString();
            }
            else if (await TryCancelRequestedOwnedLeaseAsync(job.Id, stop))
            {
                finalMetricStatus = DurableJobStatus.Cancelled.ToString();
            }
            else
            {
                finalMetricStatus = "StaleDiscarded";
                logger.LogWarning(
                    ex,
                    "Durable job {JobId} timed out after ownership changed; stale failure state was discarded.",
                    job.Id);
            }
        }
        catch (Exception ex)
        {
            if (await TryFailOwnedLeaseAsync(job.Id, "handler-failure", ex.Message, false, stop))
            {
                finalMetricStatus = DurableJobStatus.Failed.ToString();
            }
            else if (await TryCancelRequestedOwnedLeaseAsync(job.Id, stop))
            {
                finalMetricStatus = DurableJobStatus.Cancelled.ToString();
            }
            else
            {
                finalMetricStatus = "StaleDiscarded";
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
                finalMetricStatus,
                executionStopwatch.Elapsed);

            requestedCancellation.Cancel();
            leaseLostCancellation.Cancel();
            try
            {
                await monitor;
            }
            catch (OperationCanceledException)
            {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
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
                var state = await ReadLeaseStateAsync(jobId, ct);
                if (state is null)
                {
                    logger.LogError(
                        "Durable job {JobId} disappeared while running; cancelling stale execution.",
                        jobId);
                    leaseLostCancellation.Cancel();
                    return;
                }

                if (state.CancellationRequestedAtUtc is not null)
                {
                    requestedCancellation.Cancel();
                    return;
                }

                if (state.Status != DurableJobStatus.Running
                    || !string.Equals(state.LeaseOwner, _consumer, StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "Durable job {JobId} lease ownership changed from {Consumer} to {Owner} " +
                        "with status {Status}; cancelling stale execution.",
                        jobId,
                        _consumer,
                        state.LeaseOwner ?? "none",
                        state.Status);
                    leaseLostCancellation.Cancel();
                    return;
                }

                if (!await TryHeartbeatOwnedLeaseAsync(jobId, lease, ct))
                {
                    // A cancellation can race the state read. Re-check before classifying
                    // this as lease loss so cancellation retains deterministic precedence.
                    state = await ReadLeaseStateAsync(jobId, ct);
                    if (state?.CancellationRequestedAtUtc is not null)
                    {
                        requestedCancellation.Cancel();
                        return;
                    }

                    logger.LogWarning(
                        "Durable job {JobId} heartbeat lost its atomic owner/expiry predicate; " +
                        "cancelling stale execution.",
                        jobId);
                    leaseLostCancellation.Cancel();
                    return;
                }
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

    private async Task<LeaseState?> ReadLeaseStateAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        return await db.DurableJobs
            .AsNoTracking()
            .Where(x => x.Id == jobId)
            .Select(x => new LeaseState(
                x.Status,
                x.LeaseOwner,
                x.LeaseExpiresAtUtc,
                x.CancellationRequestedAtUtc))
            .SingleOrDefaultAsync(ct);
    }

    private async Task<bool> TryHeartbeatOwnedLeaseAsync(
        Guid jobId,
        TimeSpan lease,
        CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var now = DateTimeOffset.UtcNow;
        var expires = now + lease;
        var affected = await db.DurableJobs
            .Where(x => x.Id == jobId
                        && x.Status == DurableJobStatus.Running
                        && x.LeaseOwner == _consumer
                        && x.LeaseExpiresAtUtc.HasValue
                        && x.LeaseExpiresAtUtc.Value > now
                        && x.CancellationRequestedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.HeartbeatAtUtc, now)
                .SetProperty(x => x.LeaseExpiresAtUtc, expires), ct);
        return affected == 1;
    }

    private async Task<bool> TryCompleteOwnedLeaseAsync(
        Guid jobId,
        string? resultJson,
        CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var now = DateTimeOffset.UtcNow;
        var normalizedResult = NormalizeOptional(resultJson, 2_000_000);
        var affected = await db.DurableJobs
            .Where(x => x.Id == jobId
                        && x.Status == DurableJobStatus.Running
                        && x.LeaseOwner == _consumer
                        && x.LeaseExpiresAtUtc.HasValue
                        && x.LeaseExpiresAtUtc.Value > now
                        && x.CancellationRequestedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, DurableJobStatus.Succeeded)
                .SetProperty(x => x.ResultJson, normalizedResult)
                .SetProperty(x => x.ProgressPercent, 100m)
                .SetProperty(x => x.CurrentStage, "Succeeded")
                .SetProperty(x => x.FinishedAtUtc, now)
                .SetProperty(x => x.HeartbeatAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseExpiresAtUtc, (DateTimeOffset?)null), ct);
        return affected == 1;
    }

    private async Task<bool> TryFailOwnedLeaseAsync(
        Guid jobId,
        string failureCode,
        string failureMessage,
        bool timedOut,
        CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var now = DateTimeOffset.UtcNow;
        var status = timedOut ? DurableJobStatus.TimedOut : DurableJobStatus.Failed;
        var code = Trim(failureCode, 120);
        var message = Trim(failureMessage, 8000);
        var stage = status.ToString();
        var affected = await db.DurableJobs
            .Where(x => x.Id == jobId
                        && x.Status == DurableJobStatus.Running
                        && x.LeaseOwner == _consumer
                        && x.LeaseExpiresAtUtc.HasValue
                        && x.LeaseExpiresAtUtc.Value > now
                        && x.CancellationRequestedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.FailureCode, code)
                .SetProperty(x => x.FailureMessage, message)
                .SetProperty(x => x.CurrentStage, stage)
                .SetProperty(x => x.FinishedAtUtc, now)
                .SetProperty(x => x.HeartbeatAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseExpiresAtUtc, (DateTimeOffset?)null), ct);
        return affected == 1;
    }

    private async Task<bool> TryCancelRequestedOwnedLeaseAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var now = DateTimeOffset.UtcNow;
        var affected = await db.DurableJobs
            .Where(x => x.Id == jobId
                        && x.Status == DurableJobStatus.Running
                        && x.LeaseOwner == _consumer
                        && x.LeaseExpiresAtUtc.HasValue
                        && x.LeaseExpiresAtUtc.Value > now
                        && x.CancellationRequestedAtUtc != null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, DurableJobStatus.Cancelled)
                .SetProperty(x => x.CurrentStage, "Cancelled")
                .SetProperty(x => x.FinishedAtUtc, now)
                .SetProperty(x => x.HeartbeatAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseExpiresAtUtc, (DateTimeOffset?)null), ct);
        return affected == 1;
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

    private static string Trim(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }

    private static string? NormalizeOptional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private sealed record LeaseState(
        DurableJobStatus Status,
        string? LeaseOwner,
        DateTimeOffset? LeaseExpiresAtUtc,
        DateTimeOffset? CancellationRequestedAtUtc);
}
