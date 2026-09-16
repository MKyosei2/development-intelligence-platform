// Purpose: Provides the D et er mi ni st ic Lo ca lD eb ug Ag en t infrastructure implementation.
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

/// <summary>
/// Fully local, deterministic debugging engine. It never performs HTTP calls,
/// never invokes an external model, and never edits a workspace. Hypotheses are
/// generated from supplied evidence only. Patch generation is intentionally
/// conservative and returns no candidate when a deterministic rule cannot prove
/// a safe textual change.
/// </summary>
public sealed class DeterministicLocalDebugAgent : IFixProposalAgent, IAutonomousDebugAgent
{
    private const string ProviderName = "DIP.LocalIntelligence";
    private const string EngineVersion = "deterministic-rules-v1";

    private static readonly Regex GuidPattern = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record Rule(
        string Id,
        string[] Tokens,
        string Title,
        string Explanation,
        decimal Confidence,
        DebugExperimentKind[] Experiments);

    private static readonly Rule[] Rules =
    [
        new(
            "environment.python-venv",
            ["pip.__main__", "no module named pip", "python analysis dependencies failed"],
            "Generated Python environment is corrupted or incomplete",
            "The evidence contains a broken pip/venv signature. Recreate only the generated virtual environment, then rerun dependency installation before investigating application code.",
            94m,
            [DebugExperimentKind.Reproduce, DebugExperimentKind.RuntimeReplay]),
        new(
            "runtime.null-dereference",
            ["nullreferenceexception", "null reference", "object reference not set"],
            "Null dereference or missing state guard",
            "A runtime null-dereference signature is present. Inspect the first project-owned frame and the state-loading path that feeds it.",
            88m,
            [DebugExperimentKind.Reproduce, DebugExperimentKind.StaticAnalysis, DebugExperimentKind.RegressionTest]),
        new(
            "web.http-500",
            ["internal server error", "http 500", "statuscode: 500", "(500)"],
            "Server-side exception on the failing route or downstream read model",
            "The evidence reports HTTP 500. Compare the page route with each API call made during rendering and isolate the first failing server-side dependency.",
            84m,
            [DebugExperimentKind.Reproduce, DebugExperimentKind.RuntimeReplay, DebugExperimentKind.RegressionTest]),
        new(
            "runtime.readiness",
            ["connection refused", "リモート サーバーに接続できません", "actively refused", "did not become reachable"],
            "Process or dependency readiness failure",
            "The target endpoint was unreachable rather than returning an application response. Verify process lifetime, listening port, dependency readiness and startup ordering first.",
            86m,
            [DebugExperimentKind.Reproduce, DebugExperimentKind.Stress]),
        new(
            "serialization.contract",
            ["json value could not be converted", "jsonstringenumconverter", "serialization", "deserialization"],
            "Serialization contract mismatch",
            "The producer and consumer disagree on JSON shape or enum representation. Compare the concrete wire payload with the serializer options on both sides.",
            87m,
            [DebugExperimentKind.StaticAnalysis, DebugExperimentKind.RegressionTest]),
        new(
            "compile.csharp",
            ["cs0246", "cs0103", "cs1061", "build failed", "compile failed"],
            "Compile-time contract break",
            "Compiler evidence indicates a symbol/type/member contract mismatch. Resolve the first compiler error before downstream failures because later diagnostics may be cascading.",
            90m,
            [DebugExperimentKind.StaticAnalysis, DebugExperimentKind.RegressionTest]),
        new(
            "test.regression",
            ["assert", "expected", "test failed", "tests failed", "xunit"],
            "Regression localized by a failing test",
            "A test assertion is failing. Reproduce the smallest failing test and compare the current behavior with the last known-good evidence before changing implementation code.",
            80m,
            [DebugExperimentKind.Reproduce, DebugExperimentKind.RegressionTest]),
        new(
            "runtime.timeout",
            ["timeout", "timed out", "deadline exceeded"],
            "Timeout or blocked dependency",
            "The observed failure is time-bounded. Separate CPU/workload saturation from blocked I/O, deadlock, unavailable dependency and insufficient timeout configuration.",
            78m,
            [DebugExperimentKind.Reproduce, DebugExperimentKind.Stress])
    ];

    public Task<DebugHypothesisResponse?> GenerateHypothesesAsync(
        DebugHypothesisRequest request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var evidenceIds = ExtractEvidenceIds(request.EvidenceJson);
        if (evidenceIds.Count == 0)
            return Task.FromResult<DebugHypothesisResponse?>(null);

        var haystack = string.Join("\n", new[]
        {
            request.Question ?? string.Empty,
            request.EvidenceJson ?? string.Empty,
            request.RepositoryContext ?? string.Empty
        }).ToLowerInvariant();

        var matched = Rules
            .Where(rule => rule.Tokens.Any(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(rule => rule.Confidence)
            .Take(Math.Clamp(request.MaxHypotheses, 1, 10))
            .ToArray();

        if (matched.Length == 0)
        {
            matched =
            [
                new Rule(
                    "evidence.failure-cluster",
                    ["failed", "warning", "error"],
                    "Evidence cluster in the affected subsystem",
                    "Objective evidence exists but no specialized local rule matches it. Inspect the newest failed/warning evidence and its source reference before modifying code.",
                    58m,
                    [DebugExperimentKind.Reproduce, DebugExperimentKind.StaticAnalysis])
            ];
        }

        var hypotheses = matched.Select((rule, index) => new DebugHypothesis(
            $"LOCAL-{index + 1:00}-{rule.Id}",
            rule.Title,
            rule.Explanation,
            Math.Min(95m, rule.Confidence + Math.Min(5m, evidenceIds.Count - 1)),
            evidenceIds.Take(20).ToArray(),
            rule.Experiments)).ToArray();

        return Task.FromResult<DebugHypothesisResponse?>(
            new DebugHypothesisResponse(ProviderName, EngineVersion, hypotheses));
    }

    public Task<FixProposalResponse?> ProposeAsync(
        FixProposalRequest request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Deliberately fail closed. A guessed patch is more dangerous than no patch.
        // VerifiedFixService remains available for deterministic candidates produced
        // by future rule modules that can prove exact old/new text and file paths.
        return Task.FromResult<FixProposalResponse?>(null);
    }

    private static IReadOnlyList<Guid> ExtractEvidenceIds(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            return Array.Empty<Guid>();

        return GuidPattern.Matches(evidence)
            .Cast<Match>()
            .Select(match => Guid.TryParse(match.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
    }
}
