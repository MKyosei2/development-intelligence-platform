// Purpose: Coordinates focused QA governance processors while preserving the existing governance facade.
using Dip.Application.Abstractions.Observability;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed class QaGovernanceCoreEngine
{
    public const string GovernanceVersion = "2.0.0";

    private readonly IQaGovernanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly IDipOperationalMetrics _metrics;
    private readonly QaFindingLifecycleProcessor _findingLifecycle;
    private readonly QaReliabilityProcessor _reliabilityProcessor;
    private readonly QaBaselineProcessor _baselineProcessor;
    private readonly QaPolicyEvaluator _policyEvaluator;
    private readonly QaProvenanceBuilder _provenanceBuilder;
    private readonly QaHealthEvaluator _healthEvaluator;
    private readonly QaCostEvaluator _costEvaluator;
    private readonly QaBenchmarkComparisonService _benchmarkComparison;

    public QaGovernanceCoreEngine(
        IQaGovernanceRepository repository,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        IDipOperationalMetrics metrics,
        QaFindingLifecycleProcessor findingLifecycle,
        QaReliabilityProcessor reliabilityProcessor,
        QaBaselineProcessor baselineProcessor,
        QaPolicyEvaluator policyEvaluator,
        QaProvenanceBuilder provenanceBuilder,
        QaHealthEvaluator healthEvaluator,
        QaCostEvaluator costEvaluator,
        QaBenchmarkComparisonService benchmarkComparison)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _metrics = metrics;
        _findingLifecycle = findingLifecycle;
        _reliabilityProcessor = reliabilityProcessor;
        _baselineProcessor = baselineProcessor;
        _policyEvaluator = policyEvaluator;
        _provenanceBuilder = provenanceBuilder;
        _healthEvaluator = healthEvaluator;
        _costEvaluator = costEvaluator;
        _benchmarkComparison = benchmarkComparison;
    }

    public async Task ProcessCompletedRunAsync(
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var lifecycle = await _findingLifecycle.ProcessAsync(run, findings, now, ct);
        await _reliabilityProcessor.ProcessAsync(run, findings, now, ct);
        await _baselineProcessor.ObserveAsync(run, findings, now, ct);

        var policy = await _repository.GetActivePolicyAsync(run.ProjectId, ct);
        var rules = _policyEvaluator.ReadRules(policy);
        var states = await _repository.ListFindingStatesAsync(run.ProjectId, 10000, ct);
        var reliability = await _repository.ListReliabilityAsync(run.ProjectId, 5000, ct);
        var previousHealth = await _repository.ListSystemHealthAsync(run.ProjectId, 20, ct);
        var infrastructureFailures =
            previousHealth.Count(x => x.Status == QaSystemHealthStatus.Unavailable)
            + (run.Status == QaRunStatus.InfrastructureFailure ? 1 : 0);

        var cost = _costEvaluator.Measure(run, rules);
        var evaluation = _policyEvaluator.Evaluate(
            run,
            findings,
            states,
            reliability,
            lifecycle.ActiveWaivers,
            rules,
            policy,
            infrastructureFailures,
            cost.WallDurationMs,
            cost.CpuTimeMs,
            cost.ArtifactBytes,
            now);
        await _repository.AddPolicyEvaluationAsync(evaluation, ct);

        await _repository.AddProvenanceAsync(
            _provenanceBuilder.Build(
                run,
                findings,
                policy?.Version ?? "default-v1",
                now),
            ct);

        var health = _healthEvaluator.Evaluate(run, now);
        await _repository.AddSystemHealthAsync(
            new QaSystemHealthSnapshot(
                Guid.NewGuid(),
                run.ProjectId,
                run.Id,
                "qa-orchestrator",
                health.Status,
                health.Detail,
                now),
            ct);
        foreach (var snapshot in health.ToolSnapshots)
            await _repository.AddSystemHealthAsync(snapshot, ct);

        var flaky = reliability.Count(
            x => x.Classification == TestReliabilityClassification.Flaky);
        var open = states.Count(x => !QaGovernanceMatching.IsClosed(x.Status));
        var debt = Math.Clamp(
            findings.Count(x => x.Severity == Severity.Critical) * 20m
            + findings.Count(x => x.Severity == Severity.Warning) * 5m
            + flaky * 10m
            + lifecycle.ActiveWaivers.Count * 2m,
            0m,
            1000m);

        await _repository.AddTrendAsync(
            new QaQualityTrendSnapshot(
                Guid.NewGuid(),
                run.ProjectId,
                run.Id,
                open,
                findings.Count(x => x.Severity == Severity.Critical),
                findings.Count(x => x.Severity == Severity.Warning),
                flaky,
                lifecycle.ActiveWaivers.Count,
                debt,
                now),
            ct);

        await _repository.AddCostAsync(
            new QaRunCostRecord(
                Guid.NewGuid(),
                run.ProjectId,
                run.Id,
                cost.WallDurationMs,
                cost.ToolDurationMs,
                cost.ArtifactBytes,
                cost.CpuSeconds,
                cost.BudgetVerdict,
                now),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        _metrics.GovernanceRunCompleted(
            run.Status.ToString(),
            evaluation.Verdict.ToString(),
            open,
            flaky,
            lifecycle.ActiveWaivers.Count,
            health.Status.ToString());
    }

    public async Task<QaGovernanceOverviewDto> GetOverviewAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var states = await _repository.ListFindingStatesAsync(projectId, 10000, ct);
        var waivers = (await _repository.ListWaiversAsync(projectId, ct))
            .Count(x => x.IsActive(_clock.UtcNow));
        var reliability = await _repository.ListReliabilityAsync(projectId, 5000, ct);
        var baselines = await _repository.ListBaselinesAsync(projectId, 5000, ct);
        var gate = await _repository.GetLatestPolicyEvaluationAsync(projectId, ct);
        var health = (await _repository.ListSystemHealthAsync(projectId, 1, ct))
            .FirstOrDefault();
        var trends = await _repository.ListTrendsAsync(projectId, 2, ct);
        var trend = trends.FirstOrDefault();
        var previous = trends.Skip(1).FirstOrDefault();
        var delta = trend is null || previous is null
            ? 0m
            : decimal.Round(trend.TechnicalDebtScore - previous.TechnicalDebtScore, 2);
        var direction = trend is null || previous is null
            ? "Unknown"
            : delta < -0.01m
                ? "Improving"
                : delta > 0.01m ? "Worsening" : "Stable";
        var activePolicy = await _repository.GetActivePolicyAsync(projectId, ct);
        var freshness = await GetFreshnessAsync(
            projectId,
            _policyEvaluator.ReadRules(activePolicy).MaxEvidenceAgeDays,
            ct);

        return new(
            projectId,
            gate?.Verdict.ToString() ?? QaGateVerdict.Unavailable.ToString(),
            gate?.Summary ?? "No QA policy evaluation exists yet.",
            states.Count(x => !QaGovernanceMatching.IsClosed(x.Status)),
            waivers,
            reliability.Count(x => x.Classification == TestReliabilityClassification.Flaky),
            baselines.Count(x => x.Status == QaRegressionStatus.Regressed),
            health?.Status.ToString() ?? QaSystemHealthStatus.Unavailable.ToString(),
            trend?.TechnicalDebtScore ?? 0m,
            freshness.Status,
            _clock.UtcNow,
            direction,
            delta,
            freshness.Confidence,
            freshness.ConfidencePercent);
    }

    public Task<IReadOnlyList<QaFindingState>> ListFindingStatesAsync(
        Guid projectId, int take, CancellationToken ct) =>
        _repository.ListFindingStatesAsync(projectId, take, ct);

    public Task<IReadOnlyList<QaWaiver>> ListWaiversAsync(
        Guid projectId, CancellationToken ct) =>
        _repository.ListWaiversAsync(projectId, ct);

    public Task<IReadOnlyList<QaTestReliability>> ListReliabilityAsync(
        Guid projectId, int take, CancellationToken ct) =>
        _repository.ListReliabilityAsync(projectId, take, ct);

    public Task<IReadOnlyList<QaRegressionBaseline>> ListBaselinesAsync(
        Guid projectId, int take, CancellationToken ct) =>
        _repository.ListBaselinesAsync(projectId, take, ct);

    public Task<IReadOnlyList<QaSystemHealthSnapshot>> ListSystemHealthAsync(
        Guid projectId, int take, CancellationToken ct) =>
        _repository.ListSystemHealthAsync(projectId, take, ct);

    public Task<IReadOnlyList<QaQualityTrendSnapshot>> ListTrendsAsync(
        Guid projectId, int take, CancellationToken ct) =>
        _repository.ListTrendsAsync(projectId, take, ct);

    public Task<IReadOnlyList<QaOwnershipRule>> ListOwnershipRulesAsync(
        Guid projectId, CancellationToken ct) =>
        _repository.ListOwnershipRulesAsync(projectId, ct);

    public Task<QaPolicy?> GetActivePolicyAsync(Guid projectId, CancellationToken ct) =>
        _repository.GetActivePolicyAsync(projectId, ct);

    public Task<IReadOnlyList<QaTestImpactRule>> ListTestImpactRulesAsync(
        Guid projectId, CancellationToken ct) =>
        _repository.ListTestImpactRulesAsync(projectId, ct);

    public Task<IReadOnlyList<QaArtifactRetentionRule>> ListRetentionRulesAsync(
        Guid projectId, CancellationToken ct) =>
        _repository.ListRetentionRulesAsync(projectId, ct);

    public Task<IReadOnlyList<QaBugMinimizationRecord>> ListMinimizationsAsync(
        Guid projectId, int take, CancellationToken ct) =>
        _repository.ListMinimizationsAsync(projectId, take, ct);

    public async Task<IReadOnlyList<SmartTestSelectionDto>> SelectTestsAsync(
        SmartTestSelectionRequest request,
        CancellationToken ct)
    {
        var rules = await _repository.ListTestImpactRulesAsync(request.ProjectId, ct);
        return rules
            .Select(rule => new
            {
                Rule = rule,
                Paths = request.ChangedPaths
                    .Where(path => Glob(rule.PathPattern, path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .Where(x => x.Paths.Length > 0)
            .OrderByDescending(x => x.Rule.Priority)
            .Take(Math.Clamp(request.MaximumTests, 1, 500))
            .Select(x => new SmartTestSelectionDto(
                x.Rule.TestKey,
                x.Rule.CommandJson,
                x.Rule.Priority,
                x.Paths,
                $"Matched {x.Paths.Length} changed path(s) against {x.Rule.PathPattern}."))
            .ToArray();
    }

    public async Task<IReadOnlyList<TestRecommendationDto>> RecommendTestsAsync(
        Guid projectId,
        IReadOnlyList<string> changedPaths,
        CancellationToken ct)
    {
        var selected = await SelectTestsAsync(new(projectId, changedPaths, 200), ct);
        var reliability = await _repository.ListReliabilityAsync(projectId, 500, ct);
        var list = selected
            .Select(x => new TestRecommendationDto(
                x.TestKey, x.Reason, "High", x.CommandJson))
            .ToList();

        foreach (var flaky in reliability
                     .Where(x => x.Classification == TestReliabilityClassification.Flaky)
                     .OrderByDescending(x => x.FailureRatePercent)
                     .Take(20))
        {
            if (list.All(x => !x.TestKey.Equals(flaky.TestKey, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new(
                    flaky.TestKey,
                    $"Flaky history: {flaky.FailureRatePercent:0.##}% failure rate across {flaky.PassCount + flaky.FailCount} observations.",
                    "High",
                    null));
            }
        }

        if (changedPaths.Count > 0 && selected.Count == 0)
            list.Add(new(
                "coverage-gap",
                "No test-impact rule covers the changed paths. Add an ownership/test-impact mapping before release.",
                "High",
                null));

        return list;
    }

    public async Task<RootCauseCorrelationDto> CorrelateAsync(
        Guid projectId,
        string fingerprint,
        CancellationToken ct)
    {
        var state = await _repository.FindFindingStateAsync(projectId, fingerprint, ct)
            ?? throw new KeyNotFoundException("Finding state not found.");
        var baselines = await _repository.ListBaselinesAsync(projectId, 5000, ct);
        var baseline = baselines
            .Where(x =>
                x.SubjectKey.Equals(state.SubjectKey, StringComparison.OrdinalIgnoreCase)
                && x.Status == QaRegressionStatus.Regressed)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();
        var candidates = new[] { baseline?.FirstBadCommit, state.LastCommitSha }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new(
            fingerprint,
            baseline?.LastKnownGoodCommit,
            baseline?.FirstBadCommit,
            state.LastCommitSha,
            state.Team,
            state.Workstream,
            state.Occurrences,
            candidates,
            baseline is null
                ? "No regression baseline exists yet; correlation is limited to the last observed commit."
                : "Candidates are bounded by the last-known-good and first-bad QA baseline. They are suspects, not proven causes.");
    }

    public async Task<QaFreshnessDto> GetFreshnessAsync(
        Guid projectId,
        int maxAgeDays,
        CancellationToken ct)
    {
        maxAgeDays = Math.Clamp(maxAgeDays, 1, 90);
        var provenance = await _repository.GetLatestProvenanceAsync(projectId, ct);
        if (provenance is null)
        {
            return new(
                QaFreshnessStatus.Unknown.ToString(),
                int.MaxValue,
                maxAgeDays,
                null,
                null,
                "No QA provenance exists.",
                "Unverified",
                0m);
        }

        var age = Math.Max(
            0,
            (int)Math.Floor((_clock.UtcNow - provenance.CapturedAtUtc).TotalDays));
        var status = age > maxAgeDays
            ? QaFreshnessStatus.Stale
            : age > Math.Max(1, maxAgeDays / 2)
                ? QaFreshnessStatus.Aging
                : QaFreshnessStatus.Fresh;
        decimal confidence = status switch
        {
            QaFreshnessStatus.Fresh => 90m,
            QaFreshnessStatus.Aging => 65m,
            QaFreshnessStatus.Stale => 30m,
            _ => 0m
        };

        var health = (await _repository.ListSystemHealthAsync(projectId, 1, ct))
            .FirstOrDefault();
        if (health?.Status == QaSystemHealthStatus.Degraded)
            confidence = Math.Min(confidence, 55m);
        if (health?.Status == QaSystemHealthStatus.Unavailable)
            confidence = Math.Min(confidence, 15m);

        var label = confidence >= 80
            ? "High"
            : confidence >= 55 ? "Medium" : confidence > 0 ? "Low" : "Unverified";
        var reason = status == QaFreshnessStatus.Stale
            ? "Evidence is older than the configured release freshness window."
            : "Evidence age is within the configured freshness window.";
        if (health?.Status is QaSystemHealthStatus.Degraded or QaSystemHealthStatus.Unavailable)
            reason += $" QA self-health is {health.Status}, so confidence is reduced.";

        return new(
            status.ToString(),
            age,
            maxAgeDays,
            provenance.CommitSha,
            provenance.CapturedAtUtc,
            reason,
            label,
            confidence);
    }

    public static DiagnosticSchemaDto GetDiagnosticSchema() =>
        new(
            "dip.finding.v2",
            "2.0.0",
            ["SARIF 2.x", "JUnit-compatible XML", "TRX", "NUnit-compatible XML", "generic JSON", "unstructured text fallback"],
            "Normalized findings carry schema/source-format versions. New normalizers must preserve old readers or migrate explicitly; raw artifacts remain immutable.");

    public Task<QaCostSummaryDto> GetCostSummaryAsync(
        Guid projectId,
        int take,
        CancellationToken ct) =>
        _costEvaluator.GetSummaryAsync(projectId, take, ct);

    public Task<CrossProjectBenchmarkComparisonDto> CompareProjectsAsync(
        string definitionKey,
        string metricName,
        string aggregation,
        int take,
        CancellationToken ct) =>
        _benchmarkComparison.CompareProjectsAsync(
            definitionKey, metricName, aggregation, take, ct);

    internal static bool Glob(string pattern, string value) =>
        QaGovernanceMatching.Glob(pattern, value);
}
