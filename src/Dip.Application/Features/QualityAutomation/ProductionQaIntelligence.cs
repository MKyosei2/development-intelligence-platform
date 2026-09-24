// Purpose: Implements Production Qa Intelligence responsibilities for quality automation.
using System.Text.Json;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

/// <summary>
/// Production-oriented QA vocabulary layered on top of the universal language adapters.
/// It describes what the repository produces (game/plugin/media/DCC/web/system) without
/// replacing language/tool detection. Unknown products stay extensible through
/// .dip/production.json rather than being forced into a closed enum.
/// </summary>
public sealed record ProductionQaProfile(
    string Domain,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Evidence);


public sealed record ProductionMediaRegressionSpec(
    string Type,
    string BaselinePath,
    string CurrentPath,
    decimal WarningThresholdPercent = 2m,
    decimal FailureThresholdPercent = 10m,
    int VideoSampleFps = 2);

public sealed record QaFindingIntelligence(
    string DefectClass,
    string ImpactArea,
    string ReproductionStrategy,
    string OwnerRole,
    bool ReleaseBlocker,
    int RecommendedAttempts,
    decimal ConfidencePercent,
    IReadOnlyList<string> RootCauseSignals,
    IReadOnlyList<DebugExperimentKind> SuggestedExperiments);

public static class ProductionQaIntelligence
{
    private static readonly IReadOnlyDictionary<string, string[]> GenreCapabilities =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["fps"] = ["input-latency", "hit-registration", "network-rtt", "packet-loss", "frame-pacing", "camera"],
            ["tps"] = ["input-latency", "network-rtt", "packet-loss", "frame-pacing", "camera"],
            ["racing"] = ["vehicle-physics", "network-rtt", "prediction", "frame-pacing", "save-load"],
            ["rpg"] = ["save-load", "inventory", "quest-state", "streaming", "memory-soak"],
            ["action"] = ["input-latency", "animation-state", "collision", "frame-pacing", "camera"],
            ["fighting"] = ["input-latency", "rollback", "determinism", "hitbox", "frame-pacing"],
            ["rhythm"] = ["audio-sync", "input-latency", "timing-drift", "frame-pacing"],
            ["strategy"] = ["simulation-determinism", "pathfinding", "save-load", "large-state", "ai-turn"],
            ["simulation"] = ["simulation-determinism", "physics", "long-soak", "large-state", "save-load"],
            ["sports"] = ["physics", "animation-state", "network-rtt", "prediction", "frame-pacing"],
            ["mmo"] = ["network-rtt", "packet-loss", "server-authority", "large-state", "long-soak"],
            ["roguelike"] = ["procedural-seed", "determinism", "save-load", "inventory", "long-soak"],
            ["card"] = ["rules-determinism", "state-sync", "save-load", "network-rtt"],
            ["platformer"] = ["input-latency", "collision", "physics", "camera", "frame-pacing"],
            ["puzzle"] = ["rules-determinism", "save-load", "state-transition", "input"],
            ["adventure"] = ["save-load", "quest-state", "streaming", "dialogue-state", "asset-reference"]
        };

    public static ProductionQaProfile DetectProfile(QaTechnologyProfile technology, string workspace)
    {
        ArgumentNullException.ThrowIfNull(technology);
        var evidence = new List<string>();
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var domain = "software";

        if (technology.GameEngines.Count > 0)
        {
            domain = "game";
            capabilities.UnionWith(["runtime-crash", "frame-time", "shader", "asset-reference", "serialization", "scene-load"]);
            evidence.Add($"game engine: {string.Join(", ", technology.GameEngines)}");
        }

        if (SafeAny(workspace, "*.uplugin") || SafeAny(workspace, "*.plugin"))
        {
            domain = domain == "game" ? "game-plugin" : "plugin";
            capabilities.UnionWith(["descriptor", "host-version", "load-unload", "api-compatibility", "asset-reference"]);
            evidence.Add("plugin descriptor discovered");
        }

        if (SafeAny(workspace, "*.ma") || SafeAny(workspace, "*.mb") || SafeAny(workspace, "*.blend"))
        {
            if (domain == "software") domain = "dcc";
            capabilities.UnionWith(["dcc-reference", "export-import", "asset-reference", "frame-sequence"]);
            evidence.Add("DCC asset discovered");
        }

        if (SafeAny(workspace, "*.exr") || SafeAny(workspace, "*.dpx") || SafeAny(workspace, "*.wav") || SafeAny(workspace, "*.mov"))
        {
            if (domain == "software") domain = "media";
            capabilities.UnionWith(["visual-regression", "audio-regression", "frame-sequence", "publish-integrity"]);
            evidence.Add("media-production asset discovered");
        }

        if (technology.Frameworks.Any(x => x.Equals("react", StringComparison.OrdinalIgnoreCase)
                                           || x.Equals("next", StringComparison.OrdinalIgnoreCase)
                                           || x.Equals("vue", StringComparison.OrdinalIgnoreCase)
                                           || x.Equals("svelte", StringComparison.OrdinalIgnoreCase)))
        {
            if (domain == "software") domain = "web";
            capabilities.UnionWith(["api-contract", "browser-runtime", "accessibility", "route-readiness"]);
        }

        var manifest = Path.Combine(workspace, ".dip", "production.json");
        if (File.Exists(manifest))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                var root = doc.RootElement;
                if (root.TryGetProperty("domain", out var d) && d.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(d.GetString()))
                    domain = d.GetString()!.Trim();
                ReadStrings(root, "genres", genres);
                ReadStrings(root, "capabilities", capabilities);
                evidence.Add("production profile: .dip/production.json");
            }
            catch (JsonException ex)
            {
                evidence.Add($"invalid production profile: {ex.Message}");
            }
        }

        foreach (var genre in genres)
            if (GenreCapabilities.TryGetValue(genre, out var mapped))
                capabilities.UnionWith(mapped);

        if (capabilities.Count == 0)
            capabilities.UnionWith(["compile", "test", "runtime", "security", "performance"]);

        return new ProductionQaProfile(
            domain,
            genres.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            capabilities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            evidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray());
    }


    public static IReadOnlyList<ProductionMediaRegressionSpec> ReadMediaRegressionSpecs(string workspace)
    {
        var path = Path.Combine(workspace, ".dip", "production.json");
        if (!File.Exists(path)) return Array.Empty<ProductionMediaRegressionSpec>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("mediaRegression", out var rows) || rows.ValueKind != JsonValueKind.Array)
                return Array.Empty<ProductionMediaRegressionSpec>();
            var result = new List<ProductionMediaRegressionSpec>();
            foreach (var row in rows.EnumerateArray().Take(50))
            {
                if (row.ValueKind != JsonValueKind.Object) continue;
                var type = String(row, "type"); var baseline = String(row, "baselinePath"); var current = String(row, "currentPath");
                if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(baseline) || string.IsNullOrWhiteSpace(current)) continue;
                var warning = Decimal(row, "warningThresholdPercent", 2m); var failure = Decimal(row, "failureThresholdPercent", 10m);
                var fps = Int(row, "videoSampleFps", 2);
                result.Add(new(type!, baseline!, current!, Math.Clamp(warning, 0m, 100m), Math.Clamp(failure, 0m, 100m), Math.Clamp(fps, 1, 10)));
            }
            return result;
        }
        catch (JsonException)
        {
            return Array.Empty<ProductionMediaRegressionSpec>();
        }

        static string? String(JsonElement row, string name) => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
        static decimal Decimal(JsonElement row, string name, decimal fallback) => row.TryGetProperty(name, out var v) && v.TryGetDecimal(out var value) ? value : fallback;
        static int Int(JsonElement row, string name, int fallback) => row.TryGetProperty(name, out var v) && v.TryGetInt32(out var value) ? value : fallback;
    }

    public static QaFindingIntelligence Classify(QaFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return Classify(finding.Category, finding.Title, finding.Message, finding.ExceptionType, finding.Severity, finding.ReproductionStatus);
    }

    public static QaFindingIntelligence Classify(QaFindingCandidate finding, ReproductionStatus reproductionStatus = ReproductionStatus.NotRequested)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return Classify(finding.Category, finding.Title, finding.Message, finding.ExceptionType, finding.Severity, reproductionStatus);
    }

    private static QaFindingIntelligence Classify(
        string category,
        string title,
        string message,
        string? exceptionType,
        Severity severity,
        ReproductionStatus reproductionStatus)
    {
        var text = $"{category} {title} {message} {exceptionType}".ToLowerInvariant();
        var signals = new List<string>();
        string defectClass;
        string impact;
        string strategy;
        string owner;
        DebugExperimentKind[] experiments;

        if (Has(text, "deadlock", "lock inversion", "thread starvation", "hang"))
        {
            defectClass = "concurrency-deadlock"; impact = "runtime stability"; strategy = "repeat-under-load-and-capture-thread-state"; owner = "Runtime/Engine";
            signals.Add("concurrency/hang signature"); experiments = [DebugExperimentKind.Reproduce, DebugExperimentKind.Stress, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "race", "threadsanitizer", "data race"))
        {
            defectClass = "concurrency-race"; impact = "nondeterministic correctness"; strategy = "stress-with-race-instrumentation"; owner = "Runtime/Engine";
            signals.Add("race detector signature"); experiments = [DebugExperimentKind.Stress, DebugExperimentKind.Reproduce, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "use-after-free", "heap-buffer", "access violation", "segmentation fault", "memory-safety", "native-crash"))
        {
            defectClass = "memory-safety"; impact = "crash/data corruption"; strategy = "replay-with-memory-instrumentation"; owner = "Runtime/Engine";
            signals.Add("native memory-safety signature"); experiments = [DebugExperimentKind.Reproduce, DebugExperimentKind.RuntimeReplay, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "memory leak", "memory-growth", "retained memory", "out of memory"))
        {
            defectClass = "memory-growth"; impact = "long-session stability"; strategy = "soak-and-compare-memory-slope"; owner = "Runtime/Performance";
            signals.Add("memory growth signature"); experiments = [DebugExperimentKind.Stress, DebugExperimentKind.Benchmark, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "shader", "render", "framebuffer", "visual regression", "pixel"))
        {
            defectClass = "rendering-regression"; impact = "visual correctness"; strategy = "reference-image-or-frame-comparison"; owner = "Rendering/Technical Art";
            signals.Add("render/visual signature"); experiments = [DebugExperimentKind.RuntimeReplay, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "frame time", "frametime", "fps", "latency", "performance", "benchmark"))
        {
            defectClass = "performance-regression"; impact = "frame/runtime performance"; strategy = "repeat-benchmark-on-matched-environment"; owner = "Performance/Engine";
            signals.Add("performance budget signature"); experiments = [DebugExperimentKind.Benchmark, DebugExperimentKind.Reproduce];
        }
        else if (Has(text, "packet loss", "network", "socket", "rtt", "rollback", "replication"))
        {
            defectClass = "network-correctness"; impact = "online play/service reliability"; strategy = "replay-with-latency-loss-jitter-matrix"; owner = "Network/Backend";
            signals.Add("network signature"); experiments = [DebugExperimentKind.RuntimeReplay, DebugExperimentKind.Stress, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "save", "serialization", "deserialization", "schema", "migration"))
        {
            defectClass = "state-compatibility"; impact = "save/data compatibility"; strategy = "roundtrip-and-version-matrix"; owner = "Gameplay/Data";
            signals.Add("state/serialization signature"); experiments = [DebugExperimentKind.RegressionTest, DebugExperimentKind.StaticAnalysis];
        }
        else if (Has(text, "uplugin", "plugin", "host version", "load failed", "abi"))
        {
            defectClass = "plugin-compatibility"; impact = "host integration"; strategy = "load-unload-host-version-matrix"; owner = "Tools/Plugin";
            signals.Add("plugin compatibility signature"); experiments = [DebugExperimentKind.Reproduce, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "maya", "dcc", "missing reference", "asset reference", "frame sequence", "asset-integrity", "unity-meta", "unity-guid", "guid collision", "meta file"))
        {
            defectClass = "content-pipeline-integrity";
            impact = "asset/media production";
            strategy = "reference-resolution-and-publish-validation";
            owner = "Technical Art/Pipeline";
            signals.Add("content pipeline signature"); experiments = [DebugExperimentKind.StaticAnalysis, DebugExperimentKind.RuntimeReplay];
        }
        else if (Has(text, "sql", "database", "dead tuple", "transaction", "constraint"))
        {
            defectClass = "data-store-correctness"; impact = "backend data integrity"; strategy = "isolated-integration-reproduction"; owner = "Backend/Data";
            signals.Add("database signature"); experiments = [DebugExperimentKind.Reproduce, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "http 500", "internal server error", "api", "timeout"))
        {
            defectClass = "service-runtime"; impact = "service availability"; strategy = "request-replay-with-dependency-evidence"; owner = "Backend/Platform";
            signals.Add("service runtime signature"); experiments = [DebugExperimentKind.Reproduce, DebugExperimentKind.RuntimeReplay, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "compile", "syntax", "typecheck", "restore", "configure"))
        {
            defectClass = "build-contract"; impact = "build reproducibility"; strategy = "clean-build-from-fixed-revision"; owner = "Owning Engineer";
            signals.Add("build diagnostic"); experiments = [DebugExperimentKind.StaticAnalysis, DebugExperimentKind.RegressionTest];
        }
        else if (Has(text, "test", "assert", "expected"))
        {
            defectClass = "behavior-regression"; impact = "expected behavior"; strategy = "smallest-failing-test-and-regression-rerun"; owner = "Owning Engineer/QA";
            signals.Add("test failure signature"); experiments = [DebugExperimentKind.Reproduce, DebugExperimentKind.RegressionTest];
        }
        else
        {
            defectClass = "general-quality"; impact = "product confidence"; strategy = "evidence-led-reproduction"; owner = "Owning Engineer/QA";
            signals.Add("generic quality evidence"); experiments = [DebugExperimentKind.Reproduce, DebugExperimentKind.StaticAnalysis];
        }

        if (reproductionStatus == ReproductionStatus.Reproduced)
            signals.Add("failure mechanically reproduced");

        var releaseBlocker = severity == Severity.Critical
                             || defectClass is "memory-safety" or "concurrency-deadlock"
                             || reproductionStatus == ReproductionStatus.Reproduced && severity != Severity.Info;
        var attempts = severity == Severity.Critical ? 5 : defectClass.StartsWith("concurrency", StringComparison.Ordinal) ? 5 : 3;
        var confidence = reproductionStatus == ReproductionStatus.Reproduced ? 97m : severity == Severity.Critical ? 88m : 76m;

        return new QaFindingIntelligence(defectClass, impact, strategy, owner, releaseBlocker, attempts, confidence, signals, experiments);
    }

    private static bool Has(string text, params string[] tokens) => tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static void ReadStrings(JsonElement root, string property, HashSet<string> target)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array) return;
        foreach (var item in value.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                target.Add(item.GetString()!.Trim());
    }

    private static bool SafeAny(string root, string pattern)
    {
        try
        {
            return Directory.Exists(root) && Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Take(1).Any();
        }
        catch
        {
            return false;
        }
    }
}
