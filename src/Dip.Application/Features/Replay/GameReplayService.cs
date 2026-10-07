// Purpose: Orchestrates deterministic game replay as the central runtime-performance evidence path for crashes, frame pacing, memory, GPU, GC, and network metrics.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Replay;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.Jobs;
using Dip.Application.Features.Performance;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Replay;

public sealed record CreateReplaySessionCommand(Guid ProjectId, string Key, string Name, string Scene, string InputArtifactId, int RandomSeed, int DurationSeconds, string LaunchCommand, string Arguments, string? WorkingDirectory, string? BaselineCommit);
public sealed record ReplaySessionDto(Guid Id, string Key, string Name, string Scene, string InputArtifactId, int RandomSeed, int DurationSeconds, string LaunchCommand, string Arguments, string? WorkingDirectory, string? BaselineCommit, bool Enabled);
public sealed record ReplayJobRequest(Guid ProjectId, Guid SessionId, string SubjectKey, string CommitSha, string BuildId, string HardwareFingerprint, Guid? PerformanceBudgetProfileId = null);
public sealed record ReplayMetricDto(string Name, string Aggregation, decimal Value, string Unit);
public sealed record ReplayComparisonDto(string Metric, string Aggregation, decimal? Baseline, decimal Current, decimal? DifferencePercent, string Verdict);
public sealed record ReplayRunResultDto(Guid RunId, string Status, string Summary, IReadOnlyList<ReplayMetricDto> Metrics, IReadOnlyList<ReplayComparisonDto> Comparisons, string? BudgetVerdict, Guid? IncidentId);

public sealed class GameReplayService
{
    private readonly IGameReplayRepository _repo;
    private readonly IGameReplayExecutor _executor;
    private readonly DurableJobService _jobs;
    private readonly ArtifactService _artifacts;
    private readonly PerformanceBudgetService _budgets;
    private readonly IncidentAggregationService _incidents;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthService _truth;

    public GameReplayService(
        IGameReplayRepository repo,
        IGameReplayExecutor executor,
        DurableJobService jobs,
        ArtifactService artifacts,
        PerformanceBudgetService budgets,
        IncidentAggregationService incidents,
        IEvidenceRepository evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        ProjectTruthService truth)
    {
        _repo = repo;
        _executor = executor;
        _jobs = jobs;
        _artifacts = artifacts;
        _budgets = budgets;
        _incidents = incidents;
        _evidence = evidence;
        _uow = uow;
        _clock = clock;
        _truth = truth;
    }

    public async Task<ReplaySessionDto> CreateSessionAsync(CreateReplaySessionCommand command, CancellationToken ct)
    {
        if (await _repo.GetSessionByKeyAsync(command.ProjectId, command.Key, ct) is not null)
            throw new InvalidOperationException($"Replay session '{command.Key}' already exists.");
        var entity = new GameReplaySession(
            Guid.NewGuid(), command.ProjectId, command.Key, command.Name, command.Scene,
            command.InputArtifactId, command.RandomSeed, command.DurationSeconds,
            command.LaunchCommand, command.Arguments, command.WorkingDirectory,
            command.BaselineCommit, _clock.UtcNow);
        await _repo.AddSessionAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ReplaySessionDto>> ListSessionsAsync(Guid projectId, CancellationToken ct)
        => (await _repo.ListSessionsAsync(projectId, ct)).Select(Map).ToArray();

    public Task<EnqueuedJobDto> EnqueueAsync(ReplayJobRequest request, CancellationToken ct)
        => _jobs.EnqueueAsync(request.ProjectId, DurableJobType.GameSessionReplay, request.SubjectKey, request, 2, ct);

    public async Task<ReplayRunResultDto> ExecuteJobAsync(DurableJob job, ReplayJobRequest request, CancellationToken ct)
    {
        var session = await _repo.GetSessionAsync(request.SessionId, ct)
                      ?? throw new KeyNotFoundException($"Replay session {request.SessionId} was not found.");
        if (session.ProjectId != request.ProjectId) throw new InvalidOperationException("Replay session belongs to another project.");
        if (!session.Enabled) throw new InvalidOperationException("Replay session is disabled.");

        var run = new GameReplayRun(
            Guid.NewGuid(), request.ProjectId, session.Id, job.Id,
            request.CommitSha, request.BuildId, request.HardwareFingerprint, _clock.UtcNow);
        await _repo.AddRunAsync(run, ct);
        run.Start(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);

        var materialized = await MaterializeInputAsync(request.ProjectId, session.InputArtifactId, job.Id, ct);
        ReplayExecutionResult execution;
        try
        {
            execution = await _executor.ExecuteAsync(
                new ReplayExecutionRequest(
                    session.LaunchCommand,
                    session.Arguments,
                    session.WorkingDirectory,
                    session.Scene,
                    materialized.Path,
                    session.RandomSeed,
                    session.DurationSeconds,
                    request.CommitSha,
                    request.BuildId,
                    request.HardwareFingerprint),
                ct);
        }
        finally
        {
            await CleanupReplayInputDirectoryAsync(materialized.Directory, job.Id);
        }

        var metrics = Aggregate(run.Id, execution.Samples);
        if (metrics.Count > 0) await _repo.AddMetricsAsync(metrics, ct);

        var baseline = await _repo.FindBaselineRunAsync(
            request.ProjectId,
            session.Id,
            request.HardwareFingerprint,
            request.CommitSha,
            ct);
        var baselineMetrics = baseline is null
            ? Array.Empty<GameReplayMetric>()
            : await _repo.ListMetricsAsync(baseline.Id, ct);
        var comparisons = Compare(metrics, baselineMetrics);

        PerformanceBudgetEvaluationDto? budget = null;
        if (request.PerformanceBudgetProfileId.HasValue)
        {
            budget = await _budgets.EvaluateAsync(
                request.ProjectId,
                request.PerformanceBudgetProfileId.Value,
                metrics.Select(x => new PerformanceMetricInput(x.Name, x.Aggregation, x.Value, x.Unit)).ToArray(),
                run.Id,
                null,
                ct);
        }

        var regression = comparisons.Any(x => x.Verdict == "Failed");
        var success = execution.Succeeded && !execution.Crashed && !execution.TimedOut;
        var frameP95 = metrics.FirstOrDefault(x => x.Name == "frame_time_ms" && x.Aggregation == "p95")?.Value;
        var fps1Low = metrics.FirstOrDefault(x => x.Name == "fps_1pct_low" && x.Aggregation == "value")?.Value;
        var hitchRate = metrics.FirstOrDefault(x => x.Name == "hitch_rate_percent" && x.Aggregation == "value")?.Value;
        var summary =
            $"Replay {session.Key}: {(success ? "completed" : "failed")}; "
            + $"{metrics.Count} game-performance metric(s); "
            + $"frameP95={(frameP95?.ToString("0.###") ?? "n/a")} ms; "
            + $"fps1Low={(fps1Low?.ToString("0.###") ?? "n/a")}; "
            + $"hitchRate={(hitchRate?.ToString("0.###") ?? "n/a")}% ; "
            + $"{(regression ? "performance regression detected" : "no deterministic regression detected")}.";

        var links = new ArtifactLinkContext(job.Id, null, null, null, run.Id);
        var resultArtifact = await _artifacts.StoreJsonAsync(
            request.ProjectId,
            "game-replay",
            "replay-result.json",
            new
            {
                session = session.Key,
                request.CommitSha,
                request.BuildId,
                request.HardwareFingerprint,
                execution.ExitCode,
                execution.Crashed,
                execution.TimedOut,
                execution.EnvironmentFingerprint,
                gamePerformanceMetricSchema = "dip.game-performance.v2",
                metrics = metrics.Select(MapMetric),
                comparisons,
                budget
            },
            links,
            "replay",
            ct);
        if (!string.IsNullOrWhiteSpace(execution.StandardOutput))
            await _artifacts.StoreJsonAsync(request.ProjectId, "game-replay-log", "stdout.json", new { stdout = execution.StandardOutput }, links, "replay", ct);
        if (!string.IsNullOrWhiteSpace(execution.StandardError))
            await _artifacts.StoreJsonAsync(request.ProjectId, "game-replay-log", "stderr.json", new { stderr = execution.StandardError }, links, "replay", ct);

        Guid? incidentId = null;
        if (!success || regression || budget?.Verdict == PerformanceBudgetVerdict.Failed.ToString())
        {
            var severity = execution.Crashed || execution.TimedOut ? Severity.Critical : Severity.Warning;
            var incident = await _incidents.ObserveSyntheticAsync(
                request.ProjectId,
                request.SubjectKey,
                $"replay:{session.Key}:{request.HardwareFingerprint}",
                $"Game replay regression: {session.Name}",
                severity,
                JsonSerializer.Serialize(new
                {
                    request.CommitSha,
                    request.BuildId,
                    execution.EnvironmentFingerprint,
                    execution.Crashed,
                    execution.TimedOut,
                    comparisons,
                    budget
                }),
                job.Id,
                ct);
            incidentId = incident.Id;
            await _incidents.AttachReplayRegressionAsync(incident.Id, summary, new { execution.Crashed, execution.TimedOut, comparisons, budget }, ct);
            await _incidents.AttachArtifactAsync(incident.Id, resultArtifact.Id, resultArtifact.FileName, ct);
        }

        var outcome = !success
            ? EvidenceOutcome.Failed
            : regression || budget?.Verdict == PerformanceBudgetVerdict.Failed.ToString()
                ? EvidenceOutcome.Warning
                : EvidenceOutcome.Succeeded;
        await _evidence.AddAsync(new EvidenceRecord(
            Guid.NewGuid(),
            request.ProjectId,
            null,
            request.SubjectKey,
            EvidenceSourceType.Runtime,
            "game-session-replay",
            outcome,
            $"Replay {session.Key}",
            summary,
            EvidenceConfidence.High,
            resultArtifact.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                run.Id,
                request.CommitSha,
                request.BuildId,
                request.HardwareFingerprint,
                execution.EnvironmentFingerprint,
                metricSchema = "dip.game-performance.v2",
                comparisons,
                budget
            }),
            _clock.UtcNow,
            _clock.UtcNow), ct);

        run.Complete(success, JsonSerializer.Serialize(new { summary, comparisons, budget, artifact = resultArtifact.Id }), _clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        await _truth.RecalculateAsync(request.ProjectId, ct);
        return new(run.Id, run.Status.ToString(), summary, metrics.Select(MapMetric).ToArray(), comparisons, budget?.Verdict, incidentId);
    }

    private async Task<(string Path, string Directory)> MaterializeInputAsync(Guid projectId, string artifactId, Guid jobId, CancellationToken ct)
    {
        if (!Guid.TryParse(artifactId, out var id))
            throw new InvalidDataException("Game replay InputArtifactId must be a durable ArtifactRecord GUID.");
        var opened = await _artifacts.OpenAsync(id, ct);
        if (opened is null) throw new FileNotFoundException($"Replay input artifact {id} was not found.");
        var value = opened.Value;
        if (value.Record.ProjectId != projectId)
        {
            await value.Content.DisposeAsync();
            throw new InvalidOperationException("Replay input artifact belongs to another project.");
        }
        await using var content = value.Content;
        var directory = Path.Combine(Path.GetTempPath(), "dip-replay-input", jobId.ToString("N"));
        Directory.CreateDirectory(directory);
        var fileName = string.IsNullOrWhiteSpace(value.Record.FileName) ? "replay-input.json" : Path.GetFileName(value.Record.FileName);
        var path = Path.Combine(directory, fileName);
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await content.CopyToAsync(output, ct);
        return (path, directory);
    }

    private static async Task CleanupReplayInputDirectoryAsync(string directory, Guid jobId)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var warningRoot = Path.Combine(Path.GetTempPath(), "dip-replay-cleanup-warnings");
            try
            {
                Directory.CreateDirectory(warningRoot);
                await File.AppendAllTextAsync(
                    Path.Combine(warningRoot, $"{jobId:N}.log"),
                    $"{DateTimeOffset.UtcNow:O} {ex.GetType().Name}: {ex.Message}{Environment.NewLine}",
                    CancellationToken.None);
            }
            catch (Exception loggingEx) when (loggingEx is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Replay cleanup warning could not be persisted for job {0}: {1}; logging failure: {2}",
                    jobId,
                    ex.Message,
                    loggingEx.Message);
            }
        }
    }

    private static List<GameReplayMetric> Aggregate(Guid runId, IReadOnlyList<ReplaySample> samples)
    {
        var normalizedSamples = samples
            .Select(sample => sample with { Name = NormalizeMetricName(sample.Name) })
            .ToArray();
        var result = new List<GameReplayMetric>();
        foreach (var group in normalizedSamples.GroupBy(x => (x.Name, x.Unit)))
        {
            var values = group.Select(x => x.Value).OrderBy(x => x).ToArray();
            if (values.Length == 0) continue;
            result.Add(new(Guid.NewGuid(), runId, group.Key.Name, values.Average(), group.Key.Unit, "avg"));
            result.Add(new(Guid.NewGuid(), runId, group.Key.Name, Percentile(values, .50), group.Key.Unit, "p50"));
            result.Add(new(Guid.NewGuid(), runId, group.Key.Name, Percentile(values, .95), group.Key.Unit, "p95"));
            result.Add(new(Guid.NewGuid(), runId, group.Key.Name, Percentile(values, .99), group.Key.Unit, "p99"));
        }

        AddDerivedGamePerformance(runId, normalizedSamples, result);
        return result;
    }

    private static void AddDerivedGamePerformance(Guid runId, IReadOnlyList<ReplaySample> samples, ICollection<GameReplayMetric> output)
    {
        var frameTimes = samples
            .Where(x => x.Name == "frame_time_ms")
            .Select(x => x.Value)
            .Where(x => x > 0m)
            .OrderBy(x => x)
            .ToArray();
        if (frameTimes.Length > 0)
        {
            var p50 = Percentile(frameTimes, .50);
            var p99 = Percentile(frameTimes, .99);
            var hitchThreshold = Math.Max(33.333m, p50 * 2m);
            var hitches = frameTimes.Count(x => x >= hitchThreshold);
            var hitchRate = decimal.Round(hitches * 100m / frameTimes.Length, 4);
            var fps1Low = p99 <= 0m ? 0m : decimal.Round(1000m / p99, 3);
            output.Add(new(Guid.NewGuid(), runId, "hitch_count", hitches, "count", "value"));
            output.Add(new(Guid.NewGuid(), runId, "hitch_rate_percent", hitchRate, "%", "value"));
            output.Add(new(Guid.NewGuid(), runId, "fps_1pct_low", fps1Low, "fps", "value"));
        }

        var fps = samples
            .Where(x => x.Name == "fps")
            .Select(x => x.Value)
            .Where(x => x > 0m)
            .OrderBy(x => x)
            .ToArray();
        if (fps.Length > 0 && !output.Any(x => x.Name.Equals("fps_1pct_low", StringComparison.OrdinalIgnoreCase)))
            output.Add(new(Guid.NewGuid(), runId, "fps_1pct_low", Percentile(fps, .01), "fps", "value"));
    }

    private static string NormalizeMetricName(string name)
    {
        var value = name.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return value switch
        {
            "frametime" or "frame_ms" or "frame_time" => "frame_time_ms",
            "gpu_ms" or "gpu_frame_ms" => "gpu_frame_time_ms",
            "cpu_ms" or "cpu_frame_ms" => "cpu_frame_time_ms",
            "vram" or "vrammb" => "vram_mb",
            "memory" or "memorymb" or "ram_mb" => "memory_mb",
            "gc_alloc" or "gc_alloc_mb_frame" => "gc_alloc_mb_per_frame",
            "network_rtt" or "rtt" => "network_rtt_ms",
            "packetloss" => "packet_loss_percent",
            _ => value
        };
    }

    private static decimal Percentile(decimal[] sorted, double percentile)
    {
        if (sorted.Length == 1) return sorted[0];
        var position = (sorted.Length - 1) * Math.Clamp(percentile, 0d, 1d);
        var lo = (int)Math.Floor(position);
        var hi = (int)Math.Ceiling(position);
        if (lo == hi) return sorted[lo];
        var fraction = (decimal)(position - lo);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * fraction;
    }

    private static IReadOnlyList<ReplayComparisonDto> Compare(IReadOnlyList<GameReplayMetric> current, IReadOnlyList<GameReplayMetric> baseline)
    {
        var list = new List<ReplayComparisonDto>();
        foreach (var metric in current)
        {
            var baselineMetric = baseline.FirstOrDefault(x =>
                NormalizeMetricName(x.Name).Equals(metric.Name, StringComparison.OrdinalIgnoreCase)
                && x.Aggregation == metric.Aggregation
                && x.Unit == metric.Unit);
            if (baselineMetric is null || baselineMetric.Value == 0)
            {
                list.Add(new(metric.Name, metric.Aggregation, null, metric.Value, null, "NotComparable"));
                continue;
            }
            var difference = (metric.Value - baselineMetric.Value) / Math.Abs(baselineMetric.Value) * 100m;
            var lowerIsBetter = IsLowerBetter(metric.Name);
            var failed = lowerIsBetter ? difference > 10m : difference < -10m;
            var warning = lowerIsBetter ? difference > 5m : difference < -5m;
            list.Add(new(metric.Name, metric.Aggregation, baselineMetric.Value, metric.Value, decimal.Round(difference, 3), failed ? "Failed" : warning ? "Warning" : "Passed"));
        }
        return list;
    }

    private static bool IsLowerBetter(string name)
    {
        var value = name.ToLowerInvariant();
        if (value.Contains("fps")) return false;
        return value.Contains("frame")
               || value.Contains("cpu")
               || value.Contains("gpu")
               || value.Contains("memory")
               || value.Contains("ram")
               || value.Contains("vram")
               || value.Contains("rtt")
               || value.Contains("latency")
               || value.Contains("packet_loss")
               || value.Contains("gc")
               || value.Contains("hitch");
    }

    private static ReplaySessionDto Map(GameReplaySession x)
        => new(x.Id, x.Key, x.Name, x.Scene, x.InputArtifactId, x.RandomSeed, x.DurationSeconds, x.LaunchCommand, x.Arguments, x.WorkingDirectory, x.BaselineCommit, x.Enabled);

    private static ReplayMetricDto MapMetric(GameReplayMetric x)
        => new(x.Name, x.Aggregation, x.Value, x.Unit);
}
