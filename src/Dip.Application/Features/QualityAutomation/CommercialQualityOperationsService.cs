// Purpose: Implements Commercial Quality Operations Service application behavior for quality automation.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

public sealed record CommercialQaServiceLevelPolicy(decimal P0Hours = 4m, decimal P1Hours = 24m, decimal P2Hours = 72m, decimal FreshVerificationHours = 24m)
{
    public static CommercialQaServiceLevelPolicy Default { get; } = new();
}

public sealed record QaReleaseGateDto(
    string Status, string Label, string Reason, decimal DecisionConfidencePercent, decimal QualityScore, string NextAction);

public sealed record QaOperationsKpiDto(
    int OpenIssues, int P0Blockers, int P1Risks, int ReproducedOpenIssues, int SlaBreaches,
    int FailedRuns24Hours, int Runs24Hours, decimal PassRate24Hours);

public sealed record QaOperationsIssueDto(
    Guid IssueId, string Fingerprint, string Priority, string Severity, string Category, string Title, string OwnerRole,
    int OccurrenceCount, decimal AgeHours, decimal SlaHours, bool SlaBreached, bool MechanicallyReproduced,
    bool BlocksRelease, decimal ConfidencePercent, string RecommendedAction, string ResidualRisk, DateTimeOffset LastSeenAtUtc);

public sealed record QaOperationsLaneDto(
    string Key, string Title, string Status, int Count, string Description, IReadOnlyList<Guid> IssueIds);

public sealed record QaRecentRunDto(
    Guid RunId, string Status, string Language, int FindingCount, int CriticalFindingCount, int WarningFindingCount,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? FinishedAtUtc, decimal AgeHours);

public sealed record QaCoverageSummaryDto(
    string Freshness, string? LatestRunStatus, Guid? LatestRunId, DateTimeOffset? LatestRunFinishedAtUtc, decimal? LatestRunAgeHours,
    string ProductionDomain, IReadOnlyList<string> Languages, IReadOnlyList<string> GameEngines,
    IReadOnlyList<string> Adapters, IReadOnlyList<string> CrossCuttingPacks);

public sealed record QaPmDecisionBriefDto(
    string Question, string Recommendation, string Rationale, string OwnerRole, string ResidualRisk, IReadOnlyList<string> Options);

public sealed record CommercialQaCommandCenterDto(
    Guid ProjectId, QaReleaseGateDto ReleaseGate, QaOperationsKpiDto Kpis, QaCoverageSummaryDto Coverage,
    IReadOnlyList<QaOperationsLaneDto> Lanes, IReadOnlyList<QaOperationsIssueDto> PriorityQueue,
    IReadOnlyList<QaRecentRunDto> RecentRuns, QaPmDecisionBriefDto DecisionBrief, DateTimeOffset GeneratedAtUtc);

public sealed class CommercialQualityOperationsService
{
    private readonly IProjectRepository _projects;
    private readonly IPipelineRunRepository _pipelines;
    private readonly IQualityIssueRepository _issues;
    private readonly IQaFindingRepository _findings;
    private readonly IQaRunRepository _runs;
    private readonly PmEvidenceTranslator _pm;
    private readonly ISystemClock _clock;
    private readonly CommercialQaServiceLevelPolicy _policy;

    public CommercialQualityOperationsService(
        IProjectRepository projects,
        IPipelineRunRepository pipelines,
        IQualityIssueRepository issues,
        IQaFindingRepository findings,
        IQaRunRepository runs,
        PmEvidenceTranslator pm,
        ISystemClock clock,
        CommercialQaServiceLevelPolicy? policy = null)
    {
        _projects = projects; _pipelines = pipelines; _issues = issues; _findings = findings; _runs = runs; _pm = pm; _clock = clock;
        _policy = policy ?? CommercialQaServiceLevelPolicy.Default;
    }

    public async Task<CommercialQaCommandCenterDto> GetAsync(Guid projectId, int take, CancellationToken ct)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        take = Math.Clamp(take, 10, 100);
        var now = _clock.UtcNow;
        var project = await _projects.GetByIdAsync(projectId, ct) ?? throw new KeyNotFoundException($"Project {projectId} not found.");
        var issues = await _issues.ListByProjectAsync(projectId, 500, ct);
        var findings = await _findings.ListRecentByProjectAsync(projectId, 4000, ct);
        var runs = (await _runs.ListRecentAsync(projectId, 50, ct)).OrderByDescending(x => x.CreatedAtUtc).ToArray();

        var reproduced = findings
            .Where(x => x.ReproductionStatus == ReproductionStatus.Reproduced)
            .Select(x => x.Fingerprint)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var queue = issues
            .Where(x => x.ResolvedAtUtc is null)
            .Select(issue => MapIssue(issue, reproduced.Contains(issue.Fingerprint), now))
            .OrderBy(x => PriorityRank(x.Priority))
            .ThenByDescending(x => x.SlaBreached)
            .ThenByDescending(x => x.MechanicallyReproduced)
            .ThenByDescending(x => x.OccurrenceCount)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .ToArray();

        var recent24 = runs.Where(x => x.CreatedAtUtc >= now.AddHours(-24)).ToArray();
        var failed24 = recent24.Count(x => x.Status is QaRunStatus.Failed or QaRunStatus.InfrastructureFailure);
        var completed24 = recent24.Count(x => x.Status is QaRunStatus.Succeeded or QaRunStatus.Failed or QaRunStatus.Partial or QaRunStatus.InfrastructureFailure);
        var passed24 = recent24.Count(x => x.Status == QaRunStatus.Succeeded);
        var passRate = completed24 == 0 ? 0m : decimal.Round((decimal)passed24 / completed24 * 100m, 1);
        var p0 = queue.Count(x => x.Priority == "P0");
        var p1 = queue.Count(x => x.Priority == "P1");
        var sla = queue.Count(x => x.SlaBreached);
        var reproducedOpen = queue.Count(x => x.MechanicallyReproduced);
        var latest = runs.FirstOrDefault();
        decimal? latestAge = latest?.FinishedAtUtc is { } finished ? Hours(now - finished) : latest is null ? null : Hours(now - latest.CreatedAtUtc);

        // Release/promotion truth is scoped to the project's default branch. A P0
        // on an unrelated feature branch remains visible in the operational queue
        // but does not silently become default-branch release evidence.
        var qaPipelines = (await _pipelines.ListByProjectAsync(projectId, 500, ct))
            .Where(x => x.CollectorType.Equals("DIP-QA", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.FinishedAtUtc ?? x.QueuedAtUtc)
            .ToArray();
        var releaseRuns = qaPipelines.Where(x => x.Branch.Equals(project.DefaultBranch, StringComparison.OrdinalIgnoreCase)).ToArray();
        var releaseIds = releaseRuns.Select(x => x.Id).ToHashSet();
        var releaseQueue = issues
            .Where(x => x.ResolvedAtUtc is null && (releaseIds.Contains(x.PipelineRunId) || releaseIds.Contains(x.FirstSeenPipelineRunId) || releaseIds.Contains(x.LastSeenPipelineRunId)))
            .Select(issue => MapIssue(issue, reproduced.Contains(issue.Fingerprint), now))
            .OrderBy(x => PriorityRank(x.Priority)).ThenByDescending(x => x.SlaBreached).ThenByDescending(x => x.MechanicallyReproduced).ToArray();
        var releaseLatest = releaseRuns.FirstOrDefault();
        var releaseAge = releaseLatest is null ? (decimal?)null : Hours(now - (releaseLatest.FinishedAtUtc ?? releaseLatest.QueuedAtUtc));
        var releaseStatus = releaseLatest?.Status.ToString();
        if (releaseStatus == "PartiallySucceeded") releaseStatus = "Partial";
        var releaseLatestQa = releaseLatest is null ? null : await _runs.GetAsync(releaseLatest.Id, ct);
        // The exact latest default-branch QA result is authoritative for current
        // branch findings. Durable issues remain additive for SLA/triage history.
        var releaseP0 = Math.Max(releaseQueue.Count(x => x.Priority == "P0"), releaseLatestQa?.CriticalFindingCount ?? 0);
        var releaseP1 = Math.Max(releaseQueue.Count(x => x.Priority == "P1"), releaseLatestQa?.WarningFindingCount ?? 0);
        var releaseSla = releaseQueue.Count(x => x.SlaBreached);
        var qualityScore = CommercialQaPolicy.QualityScore(releaseP0, releaseP1, releaseSla, releaseStatus == "Failed" ? 1 : 0);
        var gate = CommercialQaPolicy.ReleaseGate(releaseP0, releaseP1, releaseSla, releaseStatus, releaseAge, _policy.FreshVerificationHours, releaseQueue.Any(x => x.Priority == "P0" && x.MechanicallyReproduced), qualityScore);
        gate = gate with { Reason = $"Default branch '{project.DefaultBranch}': {gate.Reason}" };
        var coverage = BuildCoverage(latest, latestAge);
        var lanes = BuildLanes(queue);
        var recentRuns = runs.Take(12).Select(x => new QaRecentRunDto(
            x.Id, x.Status.ToString(), x.Language.Value, x.FindingCount, x.CriticalFindingCount, x.WarningFindingCount,
            x.CreatedAtUtc, x.FinishedAtUtc, Hours(now - (x.FinishedAtUtc ?? x.CreatedAtUtc)))).ToArray();
        var brief = BuildBrief(gate, releaseQueue);
        var kpis = new QaOperationsKpiDto(queue.Length, p0, p1, reproducedOpen, sla, failed24, recent24.Length, passRate);

        return new(projectId, gate, kpis, coverage, lanes, queue.Take(take).ToArray(), recentRuns, brief, now);
    }

    private QaOperationsIssueDto MapIssue(QualityIssue issue, bool reproduced, DateTimeOffset now)
    {
        var candidate = new QaFindingCandidate(issue.Category, issue.Severity, "quality-issue", issue.Title, issue.Description, issue.Source, RuleId: issue.RuleId, SourceFormat: "quality-issue", SourceFormatVersion: "1.0");
        var decision = _pm.Decide(candidate, reproduced);
        var priority = decision.BlocksRelease || issue.Severity == Severity.Critical ? "P0" : issue.Severity == Severity.Warning ? "P1" : "P2";
        var slaHours = priority switch { "P0" => _policy.P0Hours, "P1" => _policy.P1Hours, _ => _policy.P2Hours };
        var age = Hours(now - issue.CreatedAtUtc);
        return new(issue.Id, issue.Fingerprint, priority, issue.Severity.ToString(), issue.Category, issue.Title, decision.OwnerRole,
            issue.OccurrenceCount, age, slaHours, age > slaHours, reproduced, decision.BlocksRelease, decision.ConfidencePercent,
            decision.RecommendedAction, decision.ResidualRisk, issue.LastSeenAtUtc);
    }

    private QaCoverageSummaryDto BuildCoverage(QaRun? latest, decimal? latestAge)
    {
        if (latest is null) return new("NoVerification", null, null, null, null, "Unknown", [], [], [], []);
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var engines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var adapters = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var packs = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var domain = "Unknown";
        if (!string.IsNullOrWhiteSpace(latest.SummaryJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(latest.SummaryJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("detected", out var detected))
                {
                    ReadStringArray(detected, "languages", languages); ReadStringArray(detected, "gameEngines", engines); ReadStringArray(detected, "GameEngines", engines);
                }
                if (root.TryGetProperty("adapters", out var adapterArray) && adapterArray.ValueKind == JsonValueKind.Array)
                    foreach (var item in adapterArray.EnumerateArray())
                        if (item.TryGetProperty("adapterId", out var id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString())) adapters.Add(id.GetString()!);
                ReadStringArray(root, "crossCuttingPacks", packs);
                if (root.TryGetProperty("production", out var production) && production.ValueKind == JsonValueKind.Object)
                {
                    if (production.TryGetProperty("domain", out var d) && d.ValueKind == JsonValueKind.String) domain = d.GetString() ?? "Unknown";
                    else if (production.TryGetProperty("Domain", out d) && d.ValueKind == JsonValueKind.String) domain = d.GetString() ?? "Unknown";
                }
            }
            catch (JsonException) {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }
        var freshness = latestAge is null ? "Unknown" : latestAge <= 6m ? "Fresh" : latestAge <= _policy.FreshVerificationHours ? "Current" : "Stale";
        return new(freshness, latest.Status.ToString(), latest.Id, latest.FinishedAtUtc, latestAge, domain,
            languages.OrderBy(x => x).ToArray(), engines.OrderBy(x => x).ToArray(), adapters.OrderBy(x => x).ToArray(), packs.OrderBy(x => x).ToArray());
    }

    private static IReadOnlyList<QaOperationsLaneDto> BuildLanes(IReadOnlyList<QaOperationsIssueDto> queue)
    {
        var blocker = queue.Where(x => x.BlocksRelease).ToArray();
        var breached = queue.Where(x => x.SlaBreached).ToArray();
        var reproduced = queue.Where(x => x.MechanicallyReproduced).ToArray();
        var content = queue.Where(x => x.OwnerRole.Contains("Technical Art", StringComparison.OrdinalIgnoreCase) || x.OwnerRole.Contains("Tools", StringComparison.OrdinalIgnoreCase) || x.Category.Contains("media", StringComparison.OrdinalIgnoreCase) || x.Category.Contains("plugin", StringComparison.OrdinalIgnoreCase) || x.Category.Contains("asset", StringComparison.OrdinalIgnoreCase)).ToArray();
        var general = queue.Where(x => !blocker.Any(y => y.IssueId == x.IssueId) && !breached.Any(y => y.IssueId == x.IssueId) && !content.Any(y => y.IssueId == x.IssueId)).ToArray();
        return [
            Lane("release-blockers","Release blockers",blocker,"Danger","P0 findings that must be cleared or explicitly scoped out before promotion."),
            Lane("sla-breached","SLA breached",breached,breached.Length>0?"Warning":"Healthy","Open findings older than the commercial response window."),
            Lane("reproduced","Mechanically reproduced",reproduced,reproduced.Length>0?"Warning":"Healthy","Failures reproduced by DIP; confidence is higher than a one-off report."),
            Lane("content-pipeline","Content / plugin pipeline",content,content.Any(x=>x.BlocksRelease)?"Danger":content.Length>0?"Attention":"Healthy","Technical-art, media and plugin integration risks."),
            Lane("triage","General triage",general,general.Length>0?"Attention":"Healthy","Remaining engineering findings requiring review, ownership or acceptance.")
        ];

        static QaOperationsLaneDto Lane(string key, string title, IReadOnlyList<QaOperationsIssueDto> items, string status, string description)
            => new(key, title, status, items.Count, description, items.Take(50).Select(x => x.IssueId).ToArray());
    }

    private static QaPmDecisionBriefDto BuildBrief(QaReleaseGateDto gate, IReadOnlyList<QaOperationsIssueDto> queue)
    {
        var top = queue.Take(3).ToArray();
        var rationale = top.Length == 0 ? gate.Reason : string.Join(" ", top.Select(x => $"{x.Priority} {x.Title} ({x.OwnerRole})."));
        var options = gate.Status switch
        {
            "Blocked" => new[] { "Fix the P0 path and rerun the same evidence-producing scenario.", "Remove/disable the affected release scope, document rollback, and require explicit risk approval.", "Delay promotion until the release blocker is objectively cleared." },
            "Hold" => new[] { "Run a fresh full QA verification before promotion.", "Resolve the highest P1/SLA-breached items and reassess the gate.", "Accept only a narrowly scoped risk with an owner, expiry and rollback plan." },
            "Ready" => new[] { "Proceed through the normal human release approval.", "Keep monitoring runtime/performance evidence after promotion." },
            _ => new[] { "Establish a current QA run before making a promotion decision." }
        };
        var residual = gate.Status == "Ready" ? "Low-to-moderate: approval still depends on the declared release scope and production monitoring." : gate.Status == "Blocked" ? "High until objective verification clears every P0 blocker." : "Moderate-to-high until verification freshness and open risk are improved.";
        return new("Can this revision be promoted to the next commercial release stage?", gate.Label, rationale, "Release Manager / PM", residual, options);
    }

    private static void ReadStringArray(JsonElement root, string name, HashSet<string> target)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return;
        foreach (var item in value.EnumerateArray()) if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) target.Add(item.GetString()!);
    }

    private static decimal Hours(TimeSpan value) => decimal.Round((decimal)Math.Max(0, value.TotalHours), 1);
    private static int PriorityRank(string priority) => priority == "P0" ? 0 : priority == "P1" ? 1 : 2;
}

public static class CommercialQaPolicy
{
    public static decimal QualityScore(int p0, int p1, int slaBreaches, int failedRuns24Hours)
        => Math.Clamp(100m - Math.Min(60m, p0 * 25m) - Math.Min(24m, p1 * 6m) - Math.Min(10m, slaBreaches * 2m) - Math.Min(12m, failedRuns24Hours * 4m), 0m, 100m);

    public static QaReleaseGateDto ReleaseGate(int p0, int p1, int slaBreaches, string? latestRunStatus, decimal? latestRunAgeHours, decimal freshVerificationHours, bool hasReproducedP0, decimal qualityScore)
    {
        if (p0 > 0)
        {
            var confidence = hasReproducedP0 ? 98m : 92m;
            return new("Blocked", "DO NOT PROMOTE", $"{p0} P0 release blocker(s) remain open{(hasReproducedP0 ? "; at least one is mechanically reproduced" : "")}.", confidence, qualityScore, "Clear every P0 blocker or remove the affected scope under explicit approval, then rerun QA.");
        }
        if (string.IsNullOrWhiteSpace(latestRunStatus))
            return new("Unknown", "NO CURRENT VERIFICATION", "No QA run is available for this project.", 65m, qualityScore, "Run the commercial QA profile before making a release decision.");
        var stale = !latestRunAgeHours.HasValue || latestRunAgeHours.Value > freshVerificationHours;
        var clean = latestRunStatus.Equals(nameof(QaRunStatus.Succeeded), StringComparison.OrdinalIgnoreCase);
        if (!clean || stale || slaBreaches > 0 || p1 >= 4)
        {
            var reasons = new List<string>();
            if (!clean) reasons.Add($"latest QA status is {latestRunStatus}");
            if (stale) reasons.Add("verification is stale");
            if (slaBreaches > 0) reasons.Add($"{slaBreaches} SLA breach(es)");
            if (p1 >= 4) reasons.Add($"{p1} P1 risks");
            return new("Hold", "HOLD FOR REVIEW", string.Join(", ", reasons) + ".", 85m, qualityScore, "Resolve the highest operational risk and produce a fresh QA run before promotion.");
        }
        return new("Ready", "READY FOR HUMAN APPROVAL", "No P0 blocker is open and the latest QA evidence is current and successful.", 93m, qualityScore, "Proceed through normal release approval; keep rollback and production monitoring ready.");
    }
}
