// Purpose: Builds risk-weighted QA plans from changed files, technology/dependency signals, Development Graph impact, and prior evidence.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

public sealed class AdaptiveQaPlanner
{
    private readonly IEvidenceRepository _evidence;
    private readonly IP2IntelligenceRepository _intelligence;
    private readonly IAdaptiveQaPlanRepository _plans;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly IChangeRiskRepository? _changeRisk;
    private readonly IDevelopmentGraphRepository? _graph;

    public AdaptiveQaPlanner(
        IEvidenceRepository evidence,
        IP2IntelligenceRepository intelligence,
        IAdaptiveQaPlanRepository plans,
        IUnitOfWork uow,
        ISystemClock clock,
        IChangeRiskRepository? changeRisk = null,
        IDevelopmentGraphRepository? graph = null)
    {
        _evidence = evidence;
        _intelligence = intelligence;
        _plans = plans;
        _uow = uow;
        _clock = clock;
        _changeRisk = changeRisk;
        _graph = graph;
    }

    public async Task<AdaptiveQaPlanDto> PlanAsync(AdaptiveQaPlanCommand command, CancellationToken ct)
    {
        var changed = command.ChangedFiles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Replace('\\', '/').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (changed.Length == 0)
            throw new ArgumentException("At least one changed file is required.", nameof(command));

        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", changed))))
            .ToLowerInvariant();
        var history = await _evidence.ListForProjectAsync(
            command.ProjectId,
            _clock.UtcNow.AddDays(-90),
            ct);
        var recentFailures = history
            .Where(x => x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning)
            .ToArray();
        var latestChangeRisk = _changeRisk is null
            ? null
            : await _changeRisk.GetLatestAsync(command.ProjectId, null, ct);
        var graphNodes = _graph is null
            ? Array.Empty<DevelopmentGraphNode>()
            : (await _graph.ListNodesAsync(command.ProjectId, null, ct)).ToArray();
        var signals = AnalyzeChangeSignals(changed, graphNodes);

        var steps = new Dictionary<string, AdaptiveQaStep>(StringComparer.OrdinalIgnoreCase);
        void Add(
            string id,
            string name,
            string category,
            string reason,
            decimal baseRisk,
            decimal minutes,
            bool required = false)
        {
            var historical = recentFailures.Count(e =>
                e.EvidenceKind.Contains(category, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(e.SourceReference)
                    && changed.Any(f =>
                        e.SourceReference!.Contains(
                            Path.GetFileName(f),
                            StringComparison.OrdinalIgnoreCase))));
            var risk = Math.Clamp(baseRisk + Math.Min(25m, historical * 4m), 0m, 100m);
            var priority = risk >= 80m ? 1 : risk >= 60m ? 2 : risk >= 40m ? 3 : 4;
            var step = new AdaptiveQaStep(
                id,
                name,
                category,
                reason + (historical > 0
                    ? $" Historical evidence contains {historical} related warning/failure(s)."
                    : string.Empty),
                risk,
                minutes,
                priority,
                required || risk >= 75m);
            if (!steps.TryGetValue(id, out var existing) || step.RiskScore > existing.RiskScore)
                steps[id] = step;
        }

        var docsOnly = changed.All(IsDocumentation);
        if (docsOnly)
        {
            Add(
                "repo-contract",
                "Repository contract checks",
                "repository",
                "Only documentation/configuration changed; full runtime QA is not justified.",
                20m,
                1m,
                true);
        }
        else
        {
            Add("build", "Build / syntax validation", "build", "Source code changed.", 60m, 3m, true);
            Add(
                "test-impact",
                "Dependency / graph test impact selection",
                "test-impact",
                signals.GraphImpactedNodeCount > 0
                    ? $"Changed files intersect {signals.GraphImpactedNodeCount} Development Graph node(s); select tests from dependency, technology, and graph impact before execution."
                    : "Select tests from changed projects, dependency manifests, technology profile, paths and symbol/name overlap before executing the targeted suite.",
                signals.GraphImpactedNodeCount > 0 ? 74m : 68m,
                2m,
                true);
            Add(
                "unit-tests",
                "Targeted automated tests",
                "test",
                "Source changes should be checked against automated tests selected by impact analysis.",
                55m,
                5m,
                true);

            if (signals.Python)
                Add("python-qa", "Python hardcoded QA", "python", "Python technology was detected in the changed set.", 65m, 4m, true);
            if (signals.CSharp)
                Add("csharp-qa", "C# hardcoded QA", "csharp", "C#/.NET technology was detected in source or dependency metadata.", 65m, 5m, true);
            if (signals.Native)
                Add("cpp-qa", "C/C++ hardcoded QA + memory safety", "cpp", "Native code, shader-adjacent source, or native build metadata changed.", 75m, 8m, true);

            if (signals.DependencyManifestChanged)
            {
                Add(
                    "dependency-contract",
                    "Dependency and supply-chain validation",
                    "security",
                    "A dependency/build manifest changed; compile, dependency, and security contracts require re-validation.",
                    76m,
                    6m,
                    true);
            }

            if (signals.Shader)
            {
                Add(
                    "shader-validation",
                    "Shader compiler validation",
                    "shader",
                    "Shader source, shader build metadata, or a graph-linked rendering component changed.",
                    84m,
                    7m,
                    true);
            }

            if (signals.AssetOrContent)
            {
                Add(
                    "asset-validation",
                    "Game/content asset validation",
                    "asset",
                    "Asset/content files or graph-linked asset nodes changed.",
                    76m,
                    6m,
                    true);
            }

            if (signals.Rendering)
            {
                Add("runtime", "Runtime reproduction", "runtime", "Rendering/gameplay impact was detected from technology, file, dependency, or graph signals.", 72m, 8m, true);
                Add("visual-regression", "Visual regression", "visual", "Rendering, shader, UI, scene, art, or graph-linked rendering scope changed.", 78m, 6m, true);
                Add("gpu-performance", "GPU/performance target profiles", "performance", "Rendering changes can affect frame time and GPU headroom.", 80m, 10m, true);
            }
            if (signals.Mobile)
            {
                Add("mobile-performance", "Mobile/low capability simulation", "performance", "Mobile platform technology or platform build metadata changed.", 85m, 12m, true);
                Add("thermal", "Thermal degradation run", "thermal", "Mobile targets are sensitive to sustained thermal throttling.", 78m, 30m);
                Add("memory-pressure", "Memory pressure run", "memory", "Mobile targets have tighter memory budgets.", 82m, 10m, true);
            }
            if (signals.Audio)
                Add("audio-regression", "Audio regression", "audio", "Audio technology/content or a graph-linked audio component changed.", 72m, 6m, true);
            if (signals.UiOrArt)
                Add("visual-ui", "UI/art visual regression", "visual", "UI/art/content or related graph nodes changed.", 70m, 5m, true);
            if (signals.Network)
            {
                Add("network-fuzz", "Protocol/API fuzzing", "fuzz", "Network/serialization/API impact was detected from source, dependency, or graph signals.", 65m, 12m);
                Add("runtime-repeat", "Runtime reliability repeats", "reliability", "Network behavior can be timing-dependent.", 62m, 10m);
                Add("service-load", "Service/load regression", "load", "Network or service-facing scope changed.", 72m, 10m);
            }
            if (signals.Multiplayer)
            {
                Add(
                    "multiplayer",
                    "Deterministic multiplayer/desync QA",
                    "multiplayer",
                    "Multiplayer, replication, RPC, lobby, matchmaking, or graph-linked netcode changed; verify common-tick state hashes under bounded network impairment.",
                    86m,
                    15m,
                    true);
            }
            if (signals.GameEngine)
            {
                Add("game-engine-contract", "Game-engine integration QA", "game-engine", "Unity/Unreal/Godot technology or graph-linked engine assets were detected.", 78m, 8m, true);
            }
            if (latestChangeRisk?.RiskLevel is ChangeRiskLevel.High or ChangeRiskLevel.Critical)
            {
                Add("game-replay", "Deterministic game-session regression replay", "runtime", $"Change Risk is {latestChangeRisk.RiskLevel} ({latestChangeRisk.RiskScore:0.#}/100).", 88m, 12m, true);
                Add("performance-budget", "Platform performance budget gate", "performance", "High-risk changes require target-platform budget evidence.", 86m, 8m, true);
                Add("high-risk-fuzz", "High-risk fuzzing", "fuzz", "High-risk change requires additional robustness evidence.", 80m, 12m, latestChangeRisk.RiskLevel == ChangeRiskLevel.Critical);
                Add("high-risk-soak", "High-risk soak/reliability", "reliability", "Critical/high change risk warrants sustained execution.", 82m, 20m, latestChangeRisk.RiskLevel == ChangeRiskLevel.Critical);
            }
        }

        var ordered = steps.Values
            .OrderBy(x => x.Priority)
            .ThenByDescending(x => x.RiskScore)
            .ThenBy(x => x.EstimatedMinutes)
            .ToList();
        var selected = new List<AdaptiveQaStep>();
        var skipped = new List<string>();
        decimal total = 0m;
        foreach (var step in ordered)
        {
            if (step.Required || total + step.EstimatedMinutes <= Math.Max(1m, command.MaxEstimatedMinutes))
            {
                selected.Add(step);
                total += step.EstimatedMinutes;
            }
            else
            {
                skipped.Add($"{step.Name}: skipped by QA time budget ({step.EstimatedMinutes:0.#} min).");
            }
        }

        var risk = selected.Count == 0
            ? 0m
            : decimal.Round(
                selected.Max(x => x.RiskScore) * .6m
                + selected.Average(x => x.RiskScore) * .4m,
                2);
        var id = Guid.NewGuid();
        var executableSteps = BuildExecutableSteps(selected, command);
        var executablePlan = new AutonomousQaExperimentPlan(
            Guid.NewGuid(),
            $"adaptive:{hash[..Math.Min(12, hash.Length)]}",
            command.SubjectKey,
            null,
            executableSteps);
        var dto = new AdaptiveQaPlanDto(id, hash, risk, total, selected, skipped, executablePlan);
        var json = JsonSerializer.Serialize(dto);

        await _plans.AddAsync(
            new AdaptiveQaPlanSnapshot(id, command.ProjectId, hash, risk, total, json, _clock.UtcNow),
            ct);
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),
            command.ProjectId,
            command.SubjectKey,
            P2IntelligenceKind.AdaptiveQaPlan,
            P2IntelligenceStatus.Informational,
            risk,
            EvidenceConfidence.High,
            $"Adaptive QA selected {selected.Count} display step(s) and {executableSteps.Count} executable experiment(s), estimated {total:0.#} minutes, risk score {risk:0.#}/100.",
            json,
            null,
            _clock.UtcNow), ct);
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),
            command.ProjectId,
            command.SubjectKey,
            P2IntelligenceKind.QaCostEstimate,
            P2IntelligenceStatus.Informational,
            null,
            EvidenceConfidence.High,
            $"Estimated QA execution cost is {total:0.#} minutes for this change set.",
            JsonSerializer.Serialize(new
            {
                estimatedMinutes = total,
                steps = selected.Select(x => new { x.Id, x.EstimatedMinutes, x.Priority }),
                experiments = executableSteps.Select(x => new { x.Id, kind = x.Kind.ToString(), x.Required }),
                changeSignals = signals
            }),
            null,
            _clock.UtcNow), ct);
        await _uow.SaveChangesAsync(ct);
        return dto;
    }

    private static IReadOnlyList<AutonomousQaExperimentStep> BuildExecutableSteps(
        IReadOnlyList<AdaptiveQaStep> selected,
        AdaptiveQaPlanCommand command)
    {
        var result = new List<AutonomousQaExperimentStep>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var display in selected)
        {
            foreach (var kind in ExperimentKinds(display.Id))
            {
                var id = $"{display.Id}:{kind}";
                if (!seen.Add(id))
                    continue;
                result.Add(AutonomousQaExperimentDefaults.Create(
                    id,
                    kind,
                    display.Reason,
                    display.Required,
                    TimeoutFor(kind, display.EstimatedMinutes)));
            }
        }

        // Every executable source-code plan must have a regression-test boundary.
        // This does not invent tests; it requires the configured QA adapters to run them.
        if (selected.Any(x => x.Id is not "repo-contract")
            && result.All(x => x.Kind != DebugExperimentKind.RegressionTest))
        {
            result.Add(AutonomousQaExperimentDefaults.Create(
                "required:regression-test",
                DebugExperimentKind.RegressionTest,
                "Any source-affecting change requires a post-change regression-test boundary.",
                true,
                900));
        }

        return result;
    }

    private static IReadOnlyList<DebugExperimentKind> ExperimentKinds(string id) => id switch
    {
        "repo-contract" => [DebugExperimentKind.StaticAnalysis],
        "build" => [DebugExperimentKind.Compile],
        "test-impact" => [DebugExperimentKind.StaticAnalysis],
        "unit-tests" => [DebugExperimentKind.UnitTest],
        "python-qa" => [DebugExperimentKind.StaticAnalysis],
        "csharp-qa" => [DebugExperimentKind.StaticAnalysis],
        "cpp-qa" => [DebugExperimentKind.StaticAnalysis, DebugExperimentKind.MemorySafety],
        "dependency-contract" => [DebugExperimentKind.Security, DebugExperimentKind.Compile],
        "shader-validation" => [DebugExperimentKind.ShaderValidation],
        "asset-validation" => [DebugExperimentKind.AssetValidation],
        "game-engine-contract" => [DebugExperimentKind.AssetValidation, DebugExperimentKind.RuntimeReplay],
        "runtime" => [DebugExperimentKind.RuntimeReplay],
        "visual-regression" => [DebugExperimentKind.VisualRegression],
        "gpu-performance" => [DebugExperimentKind.GpuPerformance],
        "mobile-performance" => [DebugExperimentKind.Benchmark],
        "thermal" => [DebugExperimentKind.Stress],
        "memory-pressure" => [DebugExperimentKind.Soak],
        "audio-regression" => [DebugExperimentKind.AudioRegression],
        "visual-ui" => [DebugExperimentKind.VisualRegression],
        "network-fuzz" => [DebugExperimentKind.Fuzz],
        "runtime-repeat" => [DebugExperimentKind.RuntimeReplay],
        "service-load" => [DebugExperimentKind.LoadTest],
        "multiplayer" => [DebugExperimentKind.Multiplayer],
        "game-replay" => [DebugExperimentKind.RuntimeReplay],
        "performance-budget" => [DebugExperimentKind.Benchmark],
        "high-risk-fuzz" => [DebugExperimentKind.Fuzz],
        "high-risk-soak" => [DebugExperimentKind.Soak],
        _ => [DebugExperimentKind.StaticAnalysis]
    };

    private static int TimeoutFor(DebugExperimentKind kind, decimal estimatedMinutes)
    {
        var estimated = (int)Math.Ceiling(Math.Max(1m, estimatedMinutes) * 60m);
        var floor = kind switch
        {
            DebugExperimentKind.Soak or DebugExperimentKind.Stress => 300,
            DebugExperimentKind.GpuPerformance or DebugExperimentKind.Multiplayer => 120,
            _ => 60
        };
        return Math.Clamp(Math.Max(floor, estimated), 30, 1800);
    }

    private static ChangeSignals AnalyzeChangeSignals(
        IReadOnlyList<string> changed,
        IReadOnlyList<DevelopmentGraphNode> graphNodes)
    {
        var relatedGraph = graphNodes
            .Where(node => GraphNodeTouchesChangedFile(node, changed))
            .ToArray();
        var graphText = string.Join(' ', relatedGraph.Select(node =>
            $"{node.Type} {node.Key} {node.Name} {node.Status} {node.ExternalReference} {node.MetadataJson}"));

        bool Ext(params string[] extensions) => changed.Any(path =>
            extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
        bool ChangedContains(params string[] tokens) => changed.Any(path => ContainsAny(path, tokens));
        bool GraphContains(params string[] tokens) => ContainsAny(graphText, tokens);

        var dependency = changed.Any(IsDependencyManifest);
        var shader = Ext(".hlsl", ".glsl", ".shader", ".compute", ".vert", ".frag", ".geom")
            || ChangedContains("shader", "renderpipeline", "material")
            || GraphContains("shader", "render", "material", "lighting");
        var asset = Ext(".prefab", ".unity", ".mat", ".fbx", ".obj", ".blend", ".ma", ".mb", ".uasset", ".umap", ".tscn", ".tres")
            || ChangedContains("assets/", "content/", "textures/", "materials/", "prefabs/", "scenes/")
            || relatedGraph.Any(x => x.Type == DevelopmentGraphNodeType.Asset)
            || GraphContains("asset", "prefab", "mesh", "texture", "scene");
        var rendering = shader || ChangedContains("render", "texture", "scene", "camera", "animation", "particle", "vfx", "lighting", "ui/")
            || GraphContains("render", "visual", "camera", "vfx", "lighting", "framebuffer");
        var mobile = ChangedContains("android", "ios", "mobile", "arm64", "vulkan", "metal")
            || GraphContains("android", "ios", "mobile", "vulkan", "metal");
        var audio = ChangedContains("audio", "sound", "music", "bgm", "se/", "voice")
            || Ext(".wav", ".mp3", ".ogg", ".flac")
            || GraphContains("audio", "sound", "voice", "music");
        var uiOrArt = asset || ChangedContains("ui/", "art/", "texture", "material", "prefab", "scene")
            || GraphContains("ui", "art", "texture", "material");
        var network = ChangedContains("network", "socket", "packet", "protocol", "serialize", "api/", "server", "grpc", "websocket", "rpc")
            || GraphContains("network", "api", "server", "protocol", "multiplayer", "socket", "rpc");
        var multiplayer = ChangedContains("multiplayer", "replication", "netcode", "lobby", "matchmaking", "rpc", "network/", "networking/")
            || GraphContains("multiplayer", "replication", "netcode", "lobby", "matchmaking", "rpc", "desync");
        var gameEngine = ChangedContains("projectsettings/", "packages/manifest.json", ".uproject", "project.godot")
            || Ext(".unity", ".prefab", ".uasset", ".umap", ".tscn")
            || GraphContains("unity", "unreal", "godot");

        return new ChangeSignals(
            Python: Ext(".py") || changed.Any(x => Path.GetFileName(x).StartsWith("requirements", StringComparison.OrdinalIgnoreCase)),
            CSharp: Ext(".cs", ".csproj", ".sln", ".props", ".targets"),
            Native: Ext(".c", ".cc", ".cpp", ".cxx", ".h", ".hpp", ".hlsl", ".glsl", ".shader", ".compute", ".vert", ".frag", ".geom") || ChangedContains("cmakelists.txt"),
            DependencyManifestChanged: dependency,
            Shader: shader,
            AssetOrContent: asset,
            Rendering: rendering,
            Mobile: mobile,
            Audio: audio,
            UiOrArt: uiOrArt,
            Network: network,
            Multiplayer: multiplayer,
            GameEngine: gameEngine,
            GraphImpactedNodeCount: relatedGraph.Length);
    }

    private static bool GraphNodeTouchesChangedFile(
        DevelopmentGraphNode node,
        IReadOnlyList<string> changed)
    {
        var haystack = $"{node.Key}\n{node.Name}\n{node.ExternalReference}\n{node.MetadataJson}";
        return changed.Any(path =>
        {
            var file = Path.GetFileName(path);
            return haystack.Contains(path, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(file)
                    && haystack.Contains(file, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static bool IsDependencyManifest(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("package.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("package-lock.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pnpm-lock.yaml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("yarn.lock", StringComparison.OrdinalIgnoreCase)
            || name.Equals("packages.lock.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Cargo.lock", StringComparison.OrdinalIgnoreCase)
            || name.Equals("go.mod", StringComparison.OrdinalIgnoreCase)
            || name.Equals("go.sum", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pom.xml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("build.gradle", StringComparison.OrdinalIgnoreCase)
            || name.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase)
            || name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("requirements", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDocumentation(string path)
    {
        var ext = Path.GetExtension(path);
        return path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || new[] { ".md", ".txt", ".rst", ".adoc" }.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string text, params string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private sealed record ChangeSignals(
        bool Python,
        bool CSharp,
        bool Native,
        bool DependencyManifestChanged,
        bool Shader,
        bool AssetOrContent,
        bool Rendering,
        bool Mobile,
        bool Audio,
        bool UiOrArt,
        bool Network,
        bool Multiplayer,
        bool GameEngine,
        int GraphImpactedNodeCount);
}
