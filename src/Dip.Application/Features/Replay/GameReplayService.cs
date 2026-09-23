// Purpose: Coordinates application behavior for this feature area.
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
    public GameReplayService(IGameReplayRepository repo, IGameReplayExecutor executor, DurableJobService jobs, ArtifactService artifacts, PerformanceBudgetService budgets, IncidentAggregationService incidents, IEvidenceRepository evidence, IUnitOfWork uow, ISystemClock clock, ProjectTruthService truth) { _repo = repo;
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
    public async Task<ReplaySessionDto> CreateSessionAsync(CreateReplaySessionCommand c, CancellationToken ct) { if (await _repo.GetSessionByKeyAsync(c.ProjectId, c.Key, ct) is not null) throw new InvalidOperationException($"Replay session '{c.Key}' already exists.");
     var e = new GameReplaySession(Guid.NewGuid(), c.ProjectId, c.Key, c.Name, c.Scene, c.InputArtifactId, c.RandomSeed, c.DurationSeconds, c.LaunchCommand, c.Arguments, c.WorkingDirectory, c.BaselineCommit, _clock.UtcNow);
     await _repo.AddSessionAsync(e, ct);
     await _uow.SaveChangesAsync(ct);
     return Map(e);
     }
    public async Task<IReadOnlyList<ReplaySessionDto>> ListSessionsAsync(Guid projectId, CancellationToken ct) => (await _repo.ListSessionsAsync(projectId, ct)).Select(Map).ToArray();
    public Task<EnqueuedJobDto> EnqueueAsync(ReplayJobRequest request, CancellationToken ct) => _jobs.EnqueueAsync(request.ProjectId, DurableJobType.GameSessionReplay, request.SubjectKey, request, 2, ct);
    public async Task<ReplayRunResultDto> ExecuteJobAsync(DurableJob job, ReplayJobRequest request, CancellationToken ct)
    {
        var session = await _repo.GetSessionAsync(request.SessionId, ct) ?? throw new KeyNotFoundException($"Replay session {request.SessionId} was not found.");
        if (session.ProjectId != request.ProjectId) throw new InvalidOperationException("Replay session belongs to another project.");
        if (!session.Enabled) throw new InvalidOperationException("Replay session is disabled.");
         var run = new GameReplayRun(Guid.NewGuid(), request.ProjectId, session.Id, job.Id, request.CommitSha, request.BuildId, request.HardwareFingerprint, _clock.UtcNow);
         await _repo.AddRunAsync(run, ct);
         run.Start(_clock.UtcNow);
         await _uow.SaveChangesAsync(ct);
        var materialized = await MaterializeInputAsync(request.ProjectId, session.InputArtifactId, job.Id, ct);
         ReplayExecutionResult execution;
         try { execution = await _executor.ExecuteAsync(new ReplayExecutionRequest(session.LaunchCommand, session.Arguments, session.WorkingDirectory, session.Scene, materialized.Path, session.RandomSeed, session.DurationSeconds, request.CommitSha, request.BuildId, request.HardwareFingerprint), ct);
         }
        finally { try { Directory.Delete(materialized.Directory, true);
         }
        catch (IOException) {
            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
        } catch (UnauthorizedAccessException) {
            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
        } }
        var metrics = Aggregate(run.Id, execution.Samples);
        if (metrics.Count > 0) await _repo.AddMetricsAsync(metrics, ct);
         var baseline = await _repo.FindBaselineRunAsync(request.ProjectId, session.Id, request.HardwareFingerprint, request.CommitSha, ct);
         var baselineMetrics = baseline is null ? Array.Empty<GameReplayMetric>() : await _repo.ListMetricsAsync(baseline.Id, ct);
         var comparisons = Compare(metrics, baselineMetrics);
        PerformanceBudgetEvaluationDto? budget = null; if (request.PerformanceBudgetProfileId.HasValue) budget = await _budgets.EvaluateAsync(request.ProjectId, request.PerformanceBudgetProfileId.Value, metrics.Select(x => new PerformanceMetricInput(x.Name, x.Aggregation, x.Value, x.Unit)).ToArray(), run.Id, null, ct);
        var regression = comparisons.Any(x => x.Verdict == "Failed");
         var success = execution.Succeeded && !execution.Crashed && !execution.TimedOut;
        var summary =
            $"Replay {session.Key}: {(success ? "completed" : "failed")}; "
            + $"{metrics.Count} aggregated metric(s); "
            + $"{(regression ? "performance regression detected" : "no deterministic regression detected")}.";
         var links = new ArtifactLinkContext(job.Id, null, null, null, run.Id);
         var resultArtifact = await _artifacts.StoreJsonAsync(request.ProjectId, "game-replay", "replay-result.json", new { session = session.Key, execution.ExitCode, execution.Crashed, execution.TimedOut, execution.EnvironmentFingerprint, metrics = metrics.Select(MapMetric), comparisons, budget }, links, "replay", ct);
        if (!string.IsNullOrWhiteSpace(execution.StandardOutput)) await _artifacts.StoreJsonAsync(request.ProjectId, "game-replay-log", "stdout.json", new { stdout = execution.StandardOutput }, links, "replay", ct);
        if (!string.IsNullOrWhiteSpace(execution.StandardError)) await _artifacts.StoreJsonAsync(request.ProjectId, "game-replay-log", "stderr.json", new { stderr = execution.StandardError }, links, "replay", ct);
        Guid? incidentId = null;
        if (!success || regression || budget?.Verdict == PerformanceBudgetVerdict.Failed.ToString()) { var severity = execution.Crashed || execution.TimedOut ? Severity.Critical : Severity.Warning;
         var incident = await _incidents.ObserveSyntheticAsync(request.ProjectId, request.SubjectKey, $"replay:{session.Key}:{request.HardwareFingerprint}", $"Game replay regression: {session.Name}", severity, JsonSerializer.Serialize(new { execution.Crashed, execution.TimedOut, comparisons, budget }), job.Id, ct);
         incidentId = incident.Id;
         await _incidents.AttachReplayRegressionAsync(incident.Id, summary, new { execution.Crashed, execution.TimedOut, comparisons, budget }, ct);
         await _incidents.AttachArtifactAsync(incident.Id, resultArtifact.Id, resultArtifact.FileName, ct);
         }
        var outcome = !success ? EvidenceOutcome.Failed : regression || budget?.Verdict == PerformanceBudgetVerdict.Failed.ToString() ? EvidenceOutcome.Warning : EvidenceOutcome.Succeeded;
         await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Runtime, "game-session-replay", outcome, $"Replay {session.Key}", summary, EvidenceConfidence.High, resultArtifact.Id.ToString(), JsonSerializer.Serialize(new { run.Id, request.CommitSha, request.HardwareFingerprint, comparisons, budget }), _clock.UtcNow, _clock.UtcNow), ct);
         run.Complete(success, JsonSerializer.Serialize(new { summary, comparisons, budget, artifact = resultArtifact.Id }), _clock.UtcNow);
         await _uow.SaveChangesAsync(ct);
         await _truth.RecalculateAsync(request.ProjectId, ct);
         return new(run.Id, run.Status.ToString(), summary, metrics.Select(MapMetric).ToArray(), comparisons, budget?.Verdict, incidentId);
    }
    private async Task<(string Path, string Directory)> MaterializeInputAsync(Guid projectId, string artifactId, Guid jobId, CancellationToken ct)
    {
        if (!Guid.TryParse(artifactId, out var id)) throw new InvalidDataException("Game replay InputArtifactId must be a durable ArtifactRecord GUID.");
        var opened = await _artifacts.OpenAsync(id, ct);
        if (opened is null) throw new FileNotFoundException($"Replay input artifact {id} was not found.");
        var openedValue = opened.Value;
        if (openedValue.Record.ProjectId != projectId) { await openedValue.Content.DisposeAsync(); throw new InvalidOperationException("Replay input artifact belongs to another project."); }
        await using var content = openedValue.Content;
        var directory = Path.Combine(Path.GetTempPath(), "dip-replay-input", jobId.ToString("N")); Directory.CreateDirectory(directory);
        var fileName = string.IsNullOrWhiteSpace(openedValue.Record.FileName) ? "replay-input.json" : Path.GetFileName(openedValue.Record.FileName);
        var path = Path.Combine(directory, fileName); await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) { await content.CopyToAsync(output, ct); }
        return (path, directory);
    }
    private static List<GameReplayMetric> Aggregate(Guid runId, IReadOnlyList<ReplaySample> samples) { var result = new List<GameReplayMetric>();
    foreach (var g in samples.GroupBy(x => (x.Name, x.Unit))) { var values = g.Select(x => x.Value).OrderBy(x => x).ToArray();
    if (values.Length == 0) continue;
     result.Add(new(Guid.NewGuid(), runId, g.Key.Name, values.Average(), g.Key.Unit, "avg"));
     result.Add(new(Guid.NewGuid(), runId, g.Key.Name, Percentile(values, .50), g.Key.Unit, "p50"));
     result.Add(new(Guid.NewGuid(), runId, g.Key.Name, Percentile(values, .95), g.Key.Unit, "p95"));
     result.Add(new(Guid.NewGuid(), runId, g.Key.Name, Percentile(values, .99), g.Key.Unit, "p99"));
     } return result;
     }
    private static decimal Percentile(decimal[] sorted, double percentile) { if (sorted.Length == 1) return sorted[0];
     var pos = (sorted.Length - 1) * percentile;
     var lo = (int)Math.Floor(pos);
     var hi = (int)Math.Ceiling(pos);
    if (lo == hi) return sorted[lo];
     var frac = (decimal)(pos - lo);
     return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
     }
    private static IReadOnlyList<ReplayComparisonDto> Compare(IReadOnlyList<GameReplayMetric> current, IReadOnlyList<GameReplayMetric> baseline) { var list = new List<ReplayComparisonDto>();
    foreach (var m in current) { var b = baseline.FirstOrDefault(x => x.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase) && x.Aggregation == m.Aggregation && x.Unit == m.Unit);
    if (b is null || b.Value == 0) { list.Add(new(m.Name, m.Aggregation, null, m.Value, null, "NotComparable"));
     continue;
     } var diff = (m.Value - b.Value) / Math.Abs(b.Value) * 100m;
     var lowerIsBetter = IsLowerBetter(m.Name);
     var failed = lowerIsBetter ? diff > 10m : diff < -10m;
     var warning = lowerIsBetter ? diff > 5m : diff < -5m;
     list.Add(new(m.Name, m.Aggregation, b.Value, m.Value, decimal.Round(diff, 3), failed ? "Failed" : warning ? "Warning" : "Passed"));
     } return list;
     }
    private static bool IsLowerBetter(string name) { var n = name.ToLowerInvariant(); return n.Contains("frame") || n.Contains("cpu") || n.Contains("gpu") || n.Contains("memory") || n.Contains("ram") || n.Contains("vram") || n.Contains("rtt") || n.Contains("latency") || n.Contains("packet_loss") || n.Contains("gc"); }
    private static ReplaySessionDto Map(GameReplaySession x) => new(x.Id, x.Key, x.Name, x.Scene, x.InputArtifactId, x.RandomSeed, x.DurationSeconds, x.LaunchCommand, x.Arguments, x.WorkingDirectory, x.BaselineCommit, x.Enabled); private static ReplayMetricDto MapMetric(GameReplayMetric x) => new(x.Name, x.Aggregation, x.Value, x.Unit);
}
