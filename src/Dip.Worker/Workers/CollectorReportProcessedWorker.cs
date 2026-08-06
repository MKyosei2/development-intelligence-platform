using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Features.Quality;
using Dip.Domain.Entities;
using Dip.Infrastructure.Messaging;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class CollectorReportProcessedWorker :
    BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQueueClient _queueClient;
    private readonly RedisQueueOptions _options;
    private readonly ILogger<CollectorReportProcessedWorker> _logger;

    private readonly string _consumerName =
        $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public CollectorReportProcessedWorker(
        IServiceScopeFactory scopeFactory,
        IQueueClient queueClient,
        IOptions<RedisQueueOptions> options,
        ILogger<CollectorReportProcessedWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queueClient = queueClient;
        _options = options.Value;
        _logger = logger;
    }

    private string ConsumerGroup =>
        $"{_options.ConsumerGroup}:quality-gate";

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var envelope = await _queueClient.ReadAsync(
                    _options.StreamName,
                    ConsumerGroup,
                    _consumerName,
                    TimeSpan.FromMilliseconds(
                        Math.Max(
                            100,
                            _options.BlockMilliseconds)),
                    stoppingToken);

                if (envelope is null)
                    continue;

                await HandleEnvelopeAsync(
                    envelope,
                    stoppingToken);
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
                    "Quality-gate worker loop failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    stoppingToken);
            }
        }
    }

    private async Task HandleEnvelopeAsync(
        QueueEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!IsCollectorReportProcessed(envelope.MessageType))
        {
            await _queueClient.AcknowledgeAsync(
                _options.StreamName,
                ConsumerGroup,
                envelope.MessageId,
                cancellationToken);

            return;
        }

        try
        {
            await ProcessAsync(
                envelope,
                cancellationToken);

            await _queueClient.AcknowledgeAsync(
                _options.StreamName,
                ConsumerGroup,
                envelope.MessageId,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "CollectorReportProcessed failed. MessageId={MessageId}; Attempt={Attempt}",
                envelope.MessageId,
                envelope.Attempt);

            var maximumDeliveries =
                Math.Max(1, _options.MaxDeliveryCount);

            if (envelope.Attempt + 1 >= maximumDeliveries)
            {
                await _queueClient.MoveToDeadLetterAsync(
                    _options.StreamName,
                    ConsumerGroup,
                    _options.DeadLetterStreamName,
                    envelope,
                    exception.Message,
                    cancellationToken);
            }
            else
            {
                await _queueClient.RetryAsync(
                    _options.StreamName,
                    ConsumerGroup,
                    envelope,
                    cancellationToken);
            }
        }
    }

    private async Task ProcessAsync(
        QueueEnvelope envelope,
        CancellationToken cancellationToken)
    {
        using var document =
            JsonDocument.Parse(envelope.PayloadJson);

        var payload = document.RootElement.TryGetProperty(
            "payload",
            out var nested)
            ? nested
            : document.RootElement;

        if (!payload.TryGetProperty(
                "pipelineRunId",
                out var runIdElement)
            || !Guid.TryParse(
                runIdElement.GetString(),
                out var pipelineRunId))
        {
            throw new InvalidDataException(
                "pipelineRunId is missing from CollectorReportProcessed.");
        }

        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<DipDbContext>();

        var evaluator = scope.ServiceProvider
            .GetRequiredService<QualityGateEvaluator>();

        var run = await db.PipelineRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == pipelineRunId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PipelineRun {pipelineRunId} was not found.");

        if (await db.QualityGateResults
            .AsNoTracking()
            .AnyAsync(
                item => item.PipelineRunId == pipelineRunId,
                cancellationToken))
        {
            return;
        }

        var metrics = await db.Metrics
            .AsNoTracking()
            .Where(item =>
                item.PipelineRunId == pipelineRunId)
            .ToListAsync(cancellationToken);

        var issues = await db.QualityIssues
            .AsNoTracking()
            .Where(item =>
                item.PipelineRunId == pipelineRunId)
            .ToListAsync(cancellationToken);

        var evaluation = evaluator.Evaluate(
            metrics,
            issues);

        var entity = QualityGateResult.Create(
            Guid.NewGuid(),
            run.ProjectId,
            pipelineRunId,
            evaluation.Status,
            evaluation.CriticalIssueCount,
            evaluation.WarningIssueCount,
            evaluation.Score,
            evaluation.Reasons,
            DateTimeOffset.UtcNow);

        db.QualityGateResults.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();

            if (await db.QualityGateResults
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.PipelineRunId == pipelineRunId,
                    cancellationToken))
            {
                return;
            }

            throw;
        }

        _logger.LogInformation(
            "Quality gate evaluated. PipelineRunId={PipelineRunId}, Status={Status}, Score={Score}",
            pipelineRunId,
            evaluation.Status,
            evaluation.Score);
    }

    private static bool IsCollectorReportProcessed(
        string messageType) =>
        string.Equals(
            messageType,
            "CollectorReportProcessed",
            StringComparison.OrdinalIgnoreCase)
        || messageType.EndsWith(
            ".CollectorReportProcessed",
            StringComparison.OrdinalIgnoreCase);
}
