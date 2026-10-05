// Purpose: Dispatches formal autonomous-QA experiment contracts to the existing deterministic QA, P2, media, benchmark, and replay runners.
using System.Text.Json;
using Dip.Application.Abstractions.Replay;
using Dip.Application.Features.Benchmarks;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.Benchmarks;
using Dip.Infrastructure.P2Intelligence;
using Dip.Infrastructure.Replay;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// AI does not execute arbitrary commands through this dispatcher. Each experiment
/// kind maps to a pre-existing bounded runner. Runner-specific configuration is
/// loaded from the step, request configuration, or .dip/autonomous-qa.json.
/// Missing configuration is NotConfigured, never PASS.
/// </summary>
public sealed class AutonomousQaExperimentDispatcher(
    ProcessExecutor executor,
    IQaWorkspaceResolver configuredWorkspaceResolver,
    IRepositoryTechnologyDetector detector,
    IQaAdapterRegistry adapters) : IAutonomousQaExperimentDispatcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<AutonomousQaExperimentResult>> ExecutePlanAsync(
        AutonomousQaExperimentPlan plan,
        AutonomousQaExperimentExecutionContext context,
        CancellationToken ct)
    {
        var results = new List<AutonomousQaExperimentResult>(plan.Steps.Count);
        foreach (var step in plan.Steps)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await ExecuteAsync(step, context, ct));
        }
        return results;
    }

    public async Task<AutonomousQaExperimentResult> ExecuteAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var workspace = ResolveWorkspace(context);
            var resolver = new ExactWorkspaceResolver(workspace);
            var configuration = ResolveConfiguration(step, context, workspace);

            return step.Kind switch
            {
                DebugExperimentKind.Compile
                    or DebugExperimentKind.StaticAnalysis
                    or DebugExperimentKind.UnitTest
                    or DebugExperimentKind.RegressionTest
                    or DebugExperimentKind.AssetValidation
                    or DebugExperimentKind.ShaderValidation
                    or DebugExperimentKind.MemorySafety
                    or DebugExperimentKind.RaceDetection
                    or DebugExperimentKind.DeadlockDetection
                    or DebugExperimentKind.Security
                    or DebugExperimentKind.Reproduce
                    => await RunAdapterQaAsync(step, context, workspace, resolver, configuration, started, ct),

                DebugExperimentKind.Fuzz
                    => await RunFuzzAsync(step, context, resolver, configuration, started, ct),
                DebugExperimentKind.Stress or DebugExperimentKind.Soak
                    => await RunSoakAsync(step, context, resolver, configuration, started, ct),
                DebugExperimentKind.Mutation
                    => await RunMutationAsync(step, context, resolver, configuration, started, ct),
                DebugExperimentKind.Bisect
                    => await RunBisectAsync(step, context, resolver, configuration, started, ct),
                DebugExperimentKind.VisualRegression
                    => await RunMediaAsync(step, context, resolver, configuration, MediaRegressionType.Visual, started, ct),
                DebugExperimentKind.VideoRegression
                    => await RunMediaAsync(step, context, resolver, configuration, MediaRegressionType.Video, started, ct),
                DebugExperimentKind.AudioRegression
                    => await RunMediaAsync(step, context, resolver, configuration, MediaRegressionType.Audio, started, ct),
                DebugExperimentKind.Benchmark or DebugExperimentKind.LoadTest
                    => await RunBenchmarkAsync(step, context, resolver, configuration, started, ct),
                DebugExperimentKind.RuntimeReplay
                    => await RunReplayAsync(step, context, workspace, configuration, started, ct),
                DebugExperimentKind.VerifyPatch
                    => Result(step, context, AutonomousQaExperimentStatus.Skipped, false,
                        "VerifyPatch is owned by VerifiedFixService and is not independently dispatched.", [], null, null, started),
                _ => Result(step, context, AutonomousQaExperimentStatus.Unavailable, false,
                    $"No bounded runner is registered for experiment kind {step.Kind}.", [], null, null, started)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result(
                step,
                context,
                AutonomousQaExperimentStatus.InfrastructureFailure,
                false,
                $"Experiment infrastructure failed: {ex.GetType().Name}: {ex.Message}",
                [],
                null,
                null,
                started);
        }
    }

    private async Task<AutonomousQaExperimentResult> RunAdapterQaAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        string workspace,
        ExactWorkspaceResolver resolver,
        string? configuration,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<GenericQaConfiguration>(configuration);
        var profile = await detector.DetectAsync(workspace, ct);
        var requested = context.Language.IsUnknown ? LanguageId.Auto : context.Language;
        var selections = adapters.Resolve(requested, profile, workspace);
        if (selections.Count == 0)
        {
            return Result(step, context, AutonomousQaExperimentStatus.Unavailable, false,
                "No QA adapter supports the detected repository technology.", [], null, null, started);
        }

        var runId = Guid.NewGuid();
        var artifactRoot = resolver.QaArtifactRoot(context.ProjectId, runId);
        var tools = new List<QaToolExecution>();
        var findings = new List<(LanguageId Language, QaFindingCandidate Finding)>();

        foreach (var selection in selections)
        {
            var childRoot = Path.Combine(artifactRoot, selection.Language.Value);
            Directory.CreateDirectory(childRoot);
            var result = await selection.Adapter.RunAsync(
                new QaExecutionCommand(
                    context.ProjectId,
                    context.SubjectKey,
                    selection.Language,
                    ".",
                    context.CommitSha,
                    config?.RuntimeExecutable,
                    config?.RuntimeArguments,
                    Math.Clamp(step.TimeoutSeconds, 30, 1800),
                    AutomaticallyReproduce: step.Kind == DebugExperimentKind.Reproduce,
                    RecordScreen: false,
                    RecordSystemAudio: false),
                workspace,
                childRoot,
                ct);
            tools.AddRange(result.Tools);
            findings.AddRange(result.Findings.Select(x => (selection.Language, x)));
        }

        var available = tools.Where(x => x.WasAvailable).ToArray();
        if (available.Length == 0)
        {
            return Result(step, context, AutonomousQaExperimentStatus.Unavailable, false,
                "QA adapters were selected, but no underlying verification tool was available.",
                [artifactRoot], artifactRoot, null, started);
        }

        var critical = findings
            .Where(x => x.Finding.Severity == Severity.Critical)
            .ToArray();
        var failedTools = available.Where(x => !x.Succeeded).ToArray();
        var status = failedTools.Length > 0 || critical.Length > 0
            ? AutonomousQaExperimentStatus.Failed
            : AutonomousQaExperimentStatus.Passed;
        var refs = new List<string> { artifactRoot };
        refs.AddRange(available.Select(x => $"tool:{x.Tool}"));
        refs.AddRange(critical.Select(x => $"finding:{QaFingerprint.Create(x.Language, x.Finding)}"));
        var summary =
            $"Executed {selections.Count} QA adapter(s), {available.Length} available tool(s), "
            + $"{failedTools.Length} failed tool(s), {critical.Length} Critical finding(s).";
        return Result(step, context, status, status == AutonomousQaExperimentStatus.Passed,
            summary, refs, artifactRoot, null, started);
    }

    private async Task<AutonomousQaExperimentResult> RunFuzzAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        ExactWorkspaceResolver resolver,
        string? configuration,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<FuzzDispatchConfiguration>(configuration);
        if (config is null || string.IsNullOrWhiteSpace(config.Executable))
            return NotConfigured(step, context, "Fuzz requires executable/arguments configuration.", started);
        var command = new FuzzCommand(
            Target(context, config.Executable, config.Arguments, config.TimeoutSeconds ?? step.TimeoutSeconds),
            config.ArgumentPlaceholder ?? "{input}",
            config.Cases ?? 100,
            config.MaxInputBytes ?? 4096,
            config.Seed ?? 1337,
            config.MinimizeFailures ?? true,
            config.MaxMinimizationRunsPerFailure ?? 24);
        var runner = new FuzzExperimentRunner(executor, resolver);
        var result = await runner.RunFuzzAsync(command, ct);
        var status = MapStatus(result.Status);
        return Result(step, context, status, status == AutonomousQaExperimentStatus.Passed,
            $"Fuzz: {result.Cases} case(s), {result.Crashes} crash(es), {result.Timeouts} timeout(s), {result.UniqueFingerprints} unique failure fingerprint(s).",
            [result.ArtifactReference, $"fuzz:fingerprints:{result.UniqueFingerprints}"], result.ArtifactReference, null, started);
    }

    private async Task<AutonomousQaExperimentResult> RunSoakAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        ExactWorkspaceResolver resolver,
        string? configuration,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<SoakDispatchConfiguration>(configuration);
        if (config is null || string.IsNullOrWhiteSpace(config.Executable))
            return NotConfigured(step, context, "Soak/Stress requires executable/arguments configuration.", started);
        var command = new SoakCommand(
            Target(context, config.Executable, config.Arguments, config.TimeoutSeconds ?? step.TimeoutSeconds),
            config.DurationSeconds ?? Math.Clamp(step.TimeoutSeconds, 5, 1800),
            config.SampleIntervalSeconds ?? 5,
            config.MemoryGrowthWarningPercent ?? 20m,
            config.PerformanceDriftWarningPercent ?? 15m);
        var runner = new SoakExperimentRunner(executor, resolver);
        var result = await runner.RunSoakAsync(command, ct);
        var status = MapStatus(result.Status);
        return Result(step, context, status, status == AutonomousQaExperimentStatus.Passed,
            $"Soak: memory {result.StartMemoryMb}->{result.EndMemoryMb} MiB ({result.MemoryGrowthPercent:+0.##;-0.##;0}%), performance drift {(result.PerformanceDriftPercent?.ToString("0.##") ?? "n/a")}%, crashed={result.Crashed}.",
            [result.ArtifactReference, $"soak:samples:{result.Samples.Count}"], result.ArtifactReference, null, started);
    }

    private async Task<AutonomousQaExperimentResult> RunMutationAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        ExactWorkspaceResolver resolver,
        string? configuration,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<MutationDispatchConfiguration>(configuration);
        if (config is null || string.IsNullOrWhiteSpace(config.Executable))
            return NotConfigured(step, context, "Mutation requires an executable and mutation configuration.", started);
        var command = new MutationCommand(
            Target(context, config.Executable, config.Arguments, config.TimeoutSeconds ?? step.TimeoutSeconds),
            config.Mutations ?? [],
            config.MaxMutations ?? 50,
            config.GenerateCommonMutations ?? true,
            config.AutoSourceFiles);
        var runner = new MutationExperimentRunner(executor, resolver);
        var result = await runner.RunMutationAsync(command, ct);
        var status = MapStatus(result.Status);
        return Result(step, context, status, status == AutonomousQaExperimentStatus.Passed,
            $"Mutation: score {result.MutationScorePercent:0.##}% ({result.Killed}/{result.Applied} killed).",
            [result.ArtifactReference, $"mutation:score:{result.MutationScorePercent:0.##}"], result.ArtifactReference, null, started);
    }

    private async Task<AutonomousQaExperimentResult> RunBisectAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        ExactWorkspaceResolver resolver,
        string? configuration,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<BisectDispatchConfiguration>(configuration);
        if (config is null
            || string.IsNullOrWhiteSpace(config.Executable)
            || string.IsNullOrWhiteSpace(config.GoodCommit)
            || string.IsNullOrWhiteSpace(config.BadCommit))
            return NotConfigured(step, context, "Bisect requires executable, GoodCommit, and BadCommit configuration.", started);
        var command = new GitBisectCommand(
            Target(context, config.Executable, config.Arguments, config.TimeoutSeconds ?? step.TimeoutSeconds),
            config.GoodCommit,
            config.BadCommit,
            config.MaxEvaluations ?? 20);
        var runner = new GitBisectRunner(executor, resolver);
        var result = await runner.FindRegressionCommitAsync(command, ct);
        var status = result.Status.Equals("Found", StringComparison.OrdinalIgnoreCase)
            ? AutonomousQaExperimentStatus.Passed
            : MapStatus(result.Status);
        return Result(step, context, status, status == AutonomousQaExperimentStatus.Passed,
            $"Bisect: status={result.Status}, firstBad={result.FirstBadCommit ?? "n/a"}, lastGood={result.LastGoodCommit ?? "n/a"}, evaluations={result.Evaluations}.",
            [result.ArtifactReference, $"bisect:first-bad:{result.FirstBadCommit ?? "unknown"}"], result.ArtifactReference, null, started);
    }

    private async Task<AutonomousQaExperimentResult> RunMediaAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        ExactWorkspaceResolver resolver,
        string? configuration,
        MediaRegressionType type,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<MediaDispatchConfiguration>(configuration);
        if (config is null
            || string.IsNullOrWhiteSpace(config.BaselinePath)
            || string.IsNullOrWhiteSpace(config.CurrentPath))
            return NotConfigured(step, context, $"{type} regression requires BaselinePath and CurrentPath configuration.", started);
        var runner = new MediaRegressionRunner(executor, resolver);
        var result = await runner.AnalyzeAsync(
            new MediaRegressionCommand(
                context.ProjectId,
                context.SubjectKey,
                ".",
                config.BaselinePath,
                config.CurrentPath,
                type,
                config.WarningThresholdPercent ?? 2m,
                config.FailureThresholdPercent ?? 10m,
                config.VideoSampleFps ?? 2),
            ct);
        var status = MapStatus(result.Status);
        return Result(step, context, status, status == AutonomousQaExperimentStatus.Passed,
            $"{type} regression: {result.Summary} Difference={result.DifferencePercent:0.###}%, confidence={result.ConfidencePercent:0.##}%.",
            [result.ArtifactReference, $"media:difference:{result.DifferencePercent:0.###}"], result.ArtifactReference, null, started);
    }

    private async Task<AutonomousQaExperimentResult> RunBenchmarkAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        ExactWorkspaceResolver resolver,
        string? configuration,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<BenchmarkExecutionSpec>(configuration);
        if (config is null || string.IsNullOrWhiteSpace(config.Target))
            return NotConfigured(step, context, "Benchmark/LoadTest requires a BenchmarkExecutionSpec configuration.", started);
        if (string.Equals(config.Mode, "http", StringComparison.OrdinalIgnoreCase))
            return NotConfigured(step, context,
                "Autonomous QA does not execute direct HTTP benchmarks because arbitrary targets would create an SSRF/network-boundary risk. Configure a bounded process-mode load runner (for example k6) instead.",
                started);
        var spec = config with
        {
            WorkspaceRelativePath = ".",
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds <= 0 ? step.TimeoutSeconds : config.TimeoutSeconds, 1, 600)
        };
        using var benchmark = new BenchmarkExecutor(executor, resolver);
        var result = await benchmark.ExecuteAsync(spec, ct);
        var passed = result.Samples.Count > 0 && result.Samples.All(x => x.Passed && !x.TimedOut);
        var status = passed ? AutonomousQaExperimentStatus.Passed : AutonomousQaExperimentStatus.Failed;
        return Result(step, context, status, passed,
            $"Benchmark: {result.Samples.Count} measured sample(s), {result.Samples.Count(x => x.Passed)} passed, {result.Samples.Count(x => x.TimedOut)} timed out.",
            [$"benchmark:samples:{result.Samples.Count}", $"environment:{result.EnvironmentFingerprint}"], null, result.EnvironmentFingerprint, started);
    }

    private async Task<AutonomousQaExperimentResult> RunReplayAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        string workspace,
        string? configuration,
        DateTimeOffset started,
        CancellationToken ct)
    {
        var config = Deserialize<ReplayDispatchConfiguration>(configuration);
        if (config is null || string.IsNullOrWhiteSpace(config.Command))
            return NotConfigured(step, context, "RuntimeReplay requires replay command configuration.", started);

        var replay = new GameReplayExecutor(
            executor,
            Options.Create(new GameReplayOptions
            {
                WorkspaceRoot = workspace,
                AllowPathLookup = config.AllowPathLookup ?? false
            }));
        var result = await replay.ExecuteAsync(
            new ReplayExecutionRequest(
                config.Command,
                config.Arguments ?? string.Empty,
                config.WorkingDirectory,
                config.Scene ?? "default",
                config.InputArtifactId ?? string.Empty,
                config.RandomSeed ?? 1337,
                Math.Clamp(config.DurationSeconds ?? Math.Min(step.TimeoutSeconds, 300), 1, 1800),
                context.CommitSha ?? "working-tree-candidate",
                config.BuildId ?? "autonomous-qa",
                config.HardwareFingerprint ?? "autonomous-qa"),
            ct);
        var passed = result.Succeeded && !result.Crashed && !result.TimedOut;
        var status = passed ? AutonomousQaExperimentStatus.Passed : AutonomousQaExperimentStatus.Failed;
        return Result(step, context, status, passed,
            $"Replay: succeeded={result.Succeeded}, crashed={result.Crashed}, timedOut={result.TimedOut}, exit={result.ExitCode}, metrics={result.Samples.Count}.",
            [$"replay:exit:{result.ExitCode}", $"replay:metrics:{result.Samples.Count}", $"environment:{result.EnvironmentFingerprint}"],
            null, result.EnvironmentFingerprint, started);
    }

    private ExperimentTarget Target(
        AutonomousQaExperimentExecutionContext context,
        string executable,
        IReadOnlyList<string>? arguments,
        int timeoutSeconds) =>
        new(
            context.ProjectId,
            context.SubjectKey,
            ".",
            executable,
            arguments,
            Math.Clamp(timeoutSeconds, 1, 1800));

    private string ResolveWorkspace(AutonomousQaExperimentExecutionContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.WorkspaceOverridePath))
        {
            var overridePath = Path.GetFullPath(context.WorkspaceOverridePath);
            if (!Directory.Exists(overridePath))
                throw new DirectoryNotFoundException(overridePath);
            return overridePath;
        }
        return configuredWorkspaceResolver.Resolve(context.WorkspaceRelativePath);
    }

    private static string? ResolveConfiguration(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        string workspace)
    {
        if (!string.IsNullOrWhiteSpace(step.ConfigurationJson))
            return step.ConfigurationJson;

        var fromRequest = ExtractExperimentConfiguration(context.ExperimentConfigurationJson, step);
        if (!string.IsNullOrWhiteSpace(fromRequest))
            return fromRequest;

        var manifest = Path.Combine(workspace, ".dip", "autonomous-qa.json");
        if (!File.Exists(manifest))
            return null;
        try
        {
            return ExtractExperimentConfiguration(File.ReadAllText(manifest), step);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? ExtractExperimentConfiguration(
        string? json,
        AutonomousQaExperimentStep step)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            var experiments = Find(root, "experiments");
            if (experiments.HasValue && experiments.Value.ValueKind == JsonValueKind.Object)
                root = experiments.Value;

            foreach (var name in new[] { step.Id, step.Kind.ToString(), step.Kind.ToString().ToLowerInvariant() })
            {
                var value = Find(root, name);
                if (value is not null)
                    return value.Value.GetRawText();
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Find(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        return null;
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AutonomousQaExperimentResult NotConfigured(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        string summary,
        DateTimeOffset started) =>
        Result(step, context, AutonomousQaExperimentStatus.NotConfigured, false, summary, [], null, null, started);

    private static AutonomousQaExperimentStatus MapStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "passed" or "succeeded" or "found" => AutonomousQaExperimentStatus.Passed,
            "warning" => AutonomousQaExperimentStatus.Warning,
            "failed" => AutonomousQaExperimentStatus.Failed,
            "infrastructurefailure" => AutonomousQaExperimentStatus.InfrastructureFailure,
            "unavailable" => AutonomousQaExperimentStatus.Unavailable,
            _ => AutonomousQaExperimentStatus.Warning
        };

    private static AutonomousQaExperimentResult Result(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        AutonomousQaExperimentStatus status,
        bool verified,
        string summary,
        IReadOnlyList<string> evidence,
        string? artifact,
        string? environment,
        DateTimeOffset started)
    {
        // A PASS without any evidence reference is downgraded. This prevents
        // silent/no-op runners from satisfying a required verification boundary.
        if (status == AutonomousQaExperimentStatus.Passed && evidence.Count == 0)
        {
            status = AutonomousQaExperimentStatus.InfrastructureFailure;
            verified = false;
            summary = "Runner reported PASS without an evidence reference. " + summary;
        }
        return new(
            step.Id,
            step.Kind,
            context.Phase,
            status,
            verified && status == AutonomousQaExperimentStatus.Passed,
            summary,
            evidence,
            artifact,
            environment,
            started,
            DateTimeOffset.UtcNow);
    }

    private sealed class ExactWorkspaceResolver(string workspace) : IQaWorkspaceResolver
    {
        private readonly string _workspace = Path.GetFullPath(workspace);

        public string Resolve(string workspaceRelativePath)
        {
            var relative = string.IsNullOrWhiteSpace(workspaceRelativePath) ? "." : workspaceRelativePath;
            if (Path.IsPathRooted(relative))
                throw new InvalidOperationException("Experiment workspace paths must remain relative to the trusted verification root.");
            var resolved = Path.GetFullPath(Path.Combine(_workspace, relative));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var prefix = _workspace.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.Equals(_workspace, comparison) && !resolved.StartsWith(prefix, comparison))
                throw new InvalidOperationException("Experiment workspace escaped the trusted verification root.");
            if (!Directory.Exists(resolved))
                throw new DirectoryNotFoundException(resolved);
            return resolved;
        }

        public string QaArtifactRoot(Guid projectId, Guid qaRunId)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "dip-autonomous-qa",
                projectId.ToString("N"),
                $"run-{qaRunId:N}");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    private sealed record GenericQaConfiguration(
        string? RuntimeExecutable = null,
        IReadOnlyList<string>? RuntimeArguments = null);

    private sealed record FuzzDispatchConfiguration(
        string Executable,
        IReadOnlyList<string>? Arguments = null,
        string? ArgumentPlaceholder = null,
        int? Cases = null,
        int? MaxInputBytes = null,
        int? Seed = null,
        bool? MinimizeFailures = null,
        int? MaxMinimizationRunsPerFailure = null,
        int? TimeoutSeconds = null);

    private sealed record SoakDispatchConfiguration(
        string Executable,
        IReadOnlyList<string>? Arguments = null,
        int? DurationSeconds = null,
        int? SampleIntervalSeconds = null,
        decimal? MemoryGrowthWarningPercent = null,
        decimal? PerformanceDriftWarningPercent = null,
        int? TimeoutSeconds = null);

    private sealed record MutationDispatchConfiguration(
        string Executable,
        IReadOnlyList<string>? Arguments = null,
        IReadOnlyList<MutationCaseSpec>? Mutations = null,
        int? MaxMutations = null,
        bool? GenerateCommonMutations = null,
        IReadOnlyList<string>? AutoSourceFiles = null,
        int? TimeoutSeconds = null);

    private sealed record BisectDispatchConfiguration(
        string Executable,
        string GoodCommit,
        string BadCommit,
        IReadOnlyList<string>? Arguments = null,
        int? MaxEvaluations = null,
        int? TimeoutSeconds = null);

    private sealed record MediaDispatchConfiguration(
        string BaselinePath,
        string CurrentPath,
        decimal? WarningThresholdPercent = null,
        decimal? FailureThresholdPercent = null,
        int? VideoSampleFps = null);

    private sealed record ReplayDispatchConfiguration(
        string Command,
        string? Arguments = null,
        string? WorkingDirectory = null,
        string? Scene = null,
        string? InputArtifactId = null,
        int? RandomSeed = null,
        int? DurationSeconds = null,
        string? BuildId = null,
        string? HardwareFingerprint = null,
        bool? AllowPathLookup = null);
}
