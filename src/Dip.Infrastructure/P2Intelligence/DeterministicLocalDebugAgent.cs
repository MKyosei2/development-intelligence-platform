// Purpose: Provides deterministic local root-cause reasoning and bounded evidence-derived fix proposals without external model calls.
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

public sealed class DeterministicLocalDebugAgent : IFixProposalAgent, IAutonomousDebugAgent
{
    private const string ProviderName = "DIP.LocalIntelligence";
    private const string EngineVersion = "deterministic-evidence-reasoner-v2";

    private static readonly Regex GuidPattern = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record Signal(
        string Id,
        string[] Positive,
        string[] Negative,
        string Title,
        string Explanation,
        decimal Weight,
        DebugExperimentKind[] Experiments);

    private static readonly Signal[] Signals =
    [
        new("compile.contract", ["cs0246","cs0103","cs1061","cs0136","cs0177","build failed","compile failed"], ["timeout","connection refused"], "Compile-time contract break", "Compiler diagnostics indicate a source/type/member contract mismatch. Resolve the first project-owned compiler diagnostic before interpreting downstream CI failures.", .94m, [DebugExperimentKind.Compile, DebugExperimentKind.RegressionTest]),
        new("runtime.null", ["nullreferenceexception","object reference not set","null dereference"], ["cs0246","compile failed"], "Null dereference or missing state guard", "Runtime evidence points to missing state/null handling. Correlate the first project-owned frame with the state-loading path and reproduce before patching.", .90m, [DebugExperimentKind.Reproduce, DebugExperimentKind.StaticAnalysis, DebugExperimentKind.RegressionTest]),
        new("async.contract-drift", ["202 accepted","queued","background job","durable job","eventual consistency","polling"], ["synchronous contract"], "Asynchronous contract drift", "Producer behavior is asynchronous but a caller/test may still assert the old synchronous terminal result. Verify enqueue semantics, session/job identity and bounded convergence.", .93m, [DebugExperimentKind.Reproduce, DebugExperimentKind.RegressionTest, DebugExperimentKind.Soak]),
        new("serialization.contract", ["json value could not be converted","serialization","deserialization","schema mismatch"], [], "Serialization contract mismatch", "Producer and consumer disagree on wire shape, enum representation or version. Capture the exact payload and compare serializer/schema versions.", .87m, [DebugExperimentKind.StaticAnalysis, DebugExperimentKind.RegressionTest]),
        new("runtime.readiness", ["connection refused","actively refused","did not become reachable","health check failed"], ["http 500"], "Process/dependency readiness failure", "The dependency was not ready or listening. Distinguish startup ordering/readiness from an application response failure.", .86m, [DebugExperimentKind.Reproduce, DebugExperimentKind.Stress]),
        new("service.timeout", ["timeout","timed out","deadline exceeded"], ["compile failed"], "Timeout, saturation, deadlock, or blocked dependency", "The failure is time-bounded. Separate CPU saturation, blocked I/O, lock cycle and dependency unavailability with repeatable timing evidence.", .82m, [DebugExperimentKind.Reproduce, DebugExperimentKind.LoadTest, DebugExperimentKind.DeadlockDetection]),
        new("concurrency.race", ["data race","threadsanitizer","race condition","non-deterministic","nondeterministic"], [], "Concurrent state race", "Evidence indicates ordering-sensitive shared-state behavior. Require dedicated race/runtime evidence instead of treating static suspicion as proof.", .91m, [DebugExperimentKind.RaceDetection, DebugExperimentKind.Stress, DebugExperimentKind.RegressionTest]),
        new("network.desync", ["desync","state hash","packet loss","rollback","replication"], [], "Distributed state divergence", "Client/server or peer state diverges under network conditions. Compare common-tick state hashes and impairment attestation.", .90m, [DebugExperimentKind.Multiplayer, DebugExperimentKind.RuntimeReplay]),
        new("gpu.performance", ["gpu_frame_ms","vram","gpu hitch","renderdoc","pix capture","nsight"], [], "GPU/rendering regression", "Frame-time, memory or capture evidence points to a GPU-side regression. Require frame telemetry and capture-level evidence before accepting a fix.", .89m, [DebugExperimentKind.GpuPerformance, DebugExperimentKind.VisualRegression]),
        new("test.regression", ["assert","expected","test failed","tests failed","xunit","pytest"], ["compile failed"], "Regression localized by a failing test", "A deterministic assertion is failing. Reproduce the smallest test and compare with last known-good evidence.", .80m, [DebugExperimentKind.Reproduce, DebugExperimentKind.RegressionTest])
    ];

    public Task<DebugHypothesisResponse?> GenerateHypothesesAsync(DebugHypothesisRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var evidenceIds = ExtractEvidenceIds(request.EvidenceJson);
        if (evidenceIds.Count == 0)
            return Task.FromResult<DebugHypothesisResponse?>(null);

        var haystack = string.Join("\n", request.Question ?? string.Empty, request.EvidenceJson ?? string.Empty, request.RepositoryContext ?? string.Empty).ToLowerInvariant();
        var ranked = Signals
            .Select(signal =>
            {
                var positive = signal.Positive.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
                var negative = signal.Negative.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
                var score = positive == 0 ? 0m : signal.Weight + Math.Min(.04m, (positive - 1) * .015m) - Math.Min(.20m, negative * .08m);
                return (signal, positive, score);
            })
            .Where(x => x.positive > 0 && x.score >= .45m)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.positive)
            .Take(Math.Clamp(request.MaxHypotheses, 1, 10))
            .ToArray();

        if (ranked.Length == 0)
        {
            var generic = new Signal("evidence.cluster", ["error"], [], "Evidence-backed failure cluster", "Objective evidence exists but no specialized local model matched it. Isolate the newest failed/warning evidence, its source reference, and the smallest deterministic reproduction before changing source.", .58m, [DebugExperimentKind.Reproduce, DebugExperimentKind.StaticAnalysis]);
            ranked = [(generic, 1, generic.Weight)];
        }

        var confidenceBonus = Math.Min(.04m, Math.Max(0, evidenceIds.Count - 1) * .01m);
        var hypotheses = ranked.Select((row, index) => new DebugHypothesis(
            $"LOCAL-{index + 1:00}-{row.signal.Id}", row.signal.Title, row.signal.Explanation,
            Math.Clamp(row.score + confidenceBonus, .01m, .97m), evidenceIds.Take(20).ToArray(), row.signal.Experiments.Distinct().ToArray())).ToArray();

        return Task.FromResult<DebugHypothesisResponse?>(new DebugHypothesisResponse(
            ProviderName, EngineVersion, hypotheses,
            new AiExecutionMetadata(ProviderName, EngineVersion, "local-evidence-reasoner-v2", DurationMs: 0, FinishReason: "deterministic", Grounded: true)));
    }

    public Task<FixProposalResponse?> ProposeAsync(FixProposalRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ids = ExtractEvidenceIds(request.EvidenceJson);
        if (ids.Count == 0 || !TryReadCandidate(request.EvidenceJson, out var summary, out var diff))
            return Task.FromResult<FixProposalResponse?>(null);
        if (!LooksLikeBoundedUnifiedDiff(diff))
            return Task.FromResult<FixProposalResponse?>(null);

        return Task.FromResult<FixProposalResponse?>(new FixProposalResponse(
            ProviderName, EngineVersion, summary, diff, Array.Empty<RegressionTestDraft>(), ids.Take(20).ToArray(),
            new AiExecutionMetadata(ProviderName, EngineVersion, "local-evidence-fix-v1", DurationMs: 0, FinishReason: "evidence-candidate", Grounded: true)));
    }

    private static bool TryReadCandidate(string evidenceJson, out string summary, out string diff)
    {
        summary = string.Empty; diff = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(evidenceJson);
            if (!doc.RootElement.TryGetProperty("localFixCandidate", out var candidate) || candidate.ValueKind != JsonValueKind.Object)
                return false;
            summary = candidate.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()?.Trim() ?? string.Empty : "Evidence-derived local fix candidate";
            diff = candidate.TryGetProperty("unifiedDiff", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : string.Empty;
            return !string.IsNullOrWhiteSpace(diff);
        }
        catch (JsonException) { return false; }
    }

    private static bool LooksLikeBoundedUnifiedDiff(string diff)
    {
        if (diff.Length is < 20 or > 256_000 || !diff.Contains("--- ", StringComparison.Ordinal) || !diff.Contains("+++ ", StringComparison.Ordinal) || !diff.Contains("@@", StringComparison.Ordinal))
            return false;
        var files = Regex.Matches(diff, @"^\+\+\+\s+(?:b/)?(?<path>[^\r\n]+)$", RegexOptions.Multiline)
            .Cast<Match>().Select(x => x.Groups["path"].Value.Trim()).Where(x => x != "/dev/null").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return files.Length is > 0 and <= 12 && files.All(x => !Path.IsPathRooted(x) && !x.Split('/', '\\').Contains(".."));
    }

    private static IReadOnlyList<Guid> ExtractEvidenceIds(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return Array.Empty<Guid>();
        return GuidPattern.Matches(evidence).Cast<Match>()
            .Select(match => Guid.TryParse(match.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToArray();
    }
}
