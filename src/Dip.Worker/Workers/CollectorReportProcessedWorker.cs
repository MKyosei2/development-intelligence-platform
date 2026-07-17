using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Features.Quality;
using Dip.Domain.Entities;
using Dip.Infrastructure.Messaging;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class CollectorReportProcessedWorker : BackgroundService
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

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var envelope = await _queueClient.ReadAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                _consumerName,
                TimeSpan.FromMilliseconds(_options.BlockMilliseconds),
                stoppingToken);

            if (envelope is null)
                continue;

            try
            {
                if (!string.Equals(
                        envelope.MessageType,
                        "CollectorReportProcessed",
                        StringComparison.OrdinalIgnoreCase)
                    && !envelope.MessageType.EndsWith(
                        ".CollectorReportProcessed",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await _queueClient.AcknowledgeAsync(
                        _options.StreamName,
                        _options.ConsumerGroup,
                        envelope.MessageId,
                        stoppingToken);
                    continue;
                }

                await ProcessAsync(envelope, stoppingToken);

                await _queueClient.AcknowledgeAsync(
                    _options.StreamName,
                    _options.ConsumerGroup,
                    envelope.MessageId,
                    stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "CollectorReportProcessed failed. MessageId={MessageId}",
                    envelope.MessageId);

                if (envelope.Attempt >= _options.MaxDeliveryCount)
                {
                    await _queueClient.MoveToDeadLetterAsync(
                        _options.StreamName,
                        _options.DeadLetterStreamName,
                        envelope,
                        exception.Message,
                        stoppingToken);
                }
            }
        }
    }

    private async Task ProcessAsync(
        QueueEnvelope envelope,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(envelope.PayloadJson);
        var payload = document.RootElement.TryGetProperty(
            "payload",
            out var nested)
            ? nested
            : document.RootElement;

        if (!payload.TryGetProperty("pipelineRunId", out var runIdElement)
            || !Guid.TryParse(runIdElement.GetString(), out var pipelineRunId))
        {
            throw new InvalidDataException(
                "pipelineRunId is missing from CollectorReportProcessed.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var evaluator =
            scope.ServiceProvider.GetRequiredService<QualityGateEvaluator>();

        var run = await db.PipelineRuns
            .SingleOrDefaultAsync(
                x => x.Id == pipelineRunId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PipelineRun {pipelineRunId} was not found.");

        var alreadyExists = await db.Set<QualityGateResult>()
            .AnyAsync(
                x => x.PipelineRunId == pipelineRunId,
                cancellationToken);

        if (alreadyExists)
            return;

        var metrics = await db.Metrics
            .Where(x => x.PipelineRunId == pipelineRunId)
            .ToListAsync(cancellationToken);

        var issues = await db.QualityIssues
            .Where(x => x.PipelineRunId == pipelineRunId)
            .ToListAsync(cancellationToken);

        var evaluation = evaluator.Evaluate(metrics, issues);

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

        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Quality gate evaluated. PipelineRunId={PipelineRunId}, Status={Status}, Score={Score}",
            pipelineRunId,
            evaluation.Status,
            evaluation.Score);
    }
}
