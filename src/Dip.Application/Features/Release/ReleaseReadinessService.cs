// Purpose: Evaluates evidence-backed release readiness for an exact candidate revision.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;

public sealed class ReleaseReadinessService
{
    private static readonly string[] ResolvedManagementStatuses =
        ["Resolved", "Closed", "Done", "Completed"];

    private static readonly string[] BlockingStatuses =
        ["Blocked", "AtRisk", "NotReady", "Failed", "Critical"];

    private static readonly string[] FailedStatuses =
        ["Failed", "Failure", "Error", "Cancelled"];

    private readonly IIncidentRepository _incidents;
    private readonly IDurableJobRepository _jobs;
    private readonly IPerformanceBudgetRepository _budgets;
    private readonly IProjectTruthRepository _truth;
    private readonly IEvidenceRepository _evidence;
    private readonly IChangeRiskRepository _risk;
    private readonly IReleaseEvidenceRepository _releaseEvidence;
    private readonly IReleaseReadinessRepository _repo;
    private readonly IDevelopmentGraphRepository _graph;
    private readonly IQaGovernanceRepository _qaGovernance;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ReleaseReadinessService(
        IIncidentRepository incidents,
        IDurableJobRepository jobs,
        IPerformanceBudgetRepository budgets,
        IProjectTruthRepository truth,
        IEvidenceRepository evidence,
        IChangeRiskRepository risk,
        IReleaseEvidenceRepository releaseEvidence,
        IReleaseReadinessRepository repo,
        IDevelopmentGraphRepository graph,
        IQaGovernanceRepository qaGovernance,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _incidents = incidents;
        _jobs = jobs;
        _budgets = budgets;
        _truth = truth;
        _evidence = evidence;
        _risk = risk;
        _releaseEvidence = releaseEvidence;
        _repo = repo;
        _graph = graph;
        _qaGovernance = qaGovernance;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ReleaseReadinessDto> EvaluateAsync(
        EvaluateReleaseReadinessRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.CommitSha))
            throw new ArgumentException("CommitSha is required.", nameof(request));

        var blockers = new List<string>();
        var warnings = new List<string>();
        var actions = new List<string>();

        await EvaluateIncidentsAsync(request, blockers, actions, ct);
        await EvaluateDurableJobsAsync(request, warnings, actions, ct);

        var budgetEvaluations = await EvaluatePerformanceBudgetsAsync(
            request,
            blockers,
            warnings,
            actions,
            ct);

        var evidence = await EvaluateFreshEvidenceAsync(
            request,
            blockers,
            actions,
            ct);

        await EvaluateQaGovernanceAsync(
            request,
            blockers,
            warnings,
            actions,
            ct);

        var truth = await EvaluateProjectTruthAsync(
            request,
            blockers,
            warnings,
            actions,
            ct);

        await EvaluateReleaseEvidenceAsync(
            request,
            warnings,
            actions,
            ct);

        await EvaluateChangeRiskAsync(
            request,
            blockers,
            warnings,
            actions,
            ct);

        await EvaluateDevelopmentGraphAsync(
            request,
            blockers,
            warnings,
            actions,
            ct);

        var decision = blockers.Count > 0
            ? ReleaseDecision.NoGo
            : warnings.Count > 0
                ? ReleaseDecision.ConditionalGo
                : ReleaseDecision.Go;

        var confidence = ReleaseReadinessPolicy.CalculateConfidence(
            evidence.Count,
            truth is not null,
            budgetEvaluations.Count > 0);

        var summary = decision switch
        {
            ReleaseDecision.Go =>
                "GO: no evidence-backed release blocker is currently open.",
            ReleaseDecision.ConditionalGo =>
                "CONDITIONAL GO: no hard blocker is open, but warnings require explicit review.",
            _ =>
                "NO-GO: one or more evidence-backed release blockers must be resolved."
        };

        var entity = new ReleaseReadinessSnapshot(
            Guid.NewGuid(),
            request.ProjectId,
            request.CommitSha,
            decision,
            confidence,
            JsonSerializer.Serialize(blockers),
            JsonSerializer.Serialize(warnings),
            JsonSerializer.Serialize(actions.Distinct()),
            summary,
            _clock.UtcNow);

        await _repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ReleaseReadinessDto?> GetLatestAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var snapshot = await _repo.GetLatestAsync(projectId, ct);
        return snapshot is null ? null : Map(snapshot);
    }

    private async Task EvaluateIncidentsAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> blockers,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var incidents = await _incidents.ListRecentAsync(request.ProjectId, 200, ct);
        var openCritical = incidents
            .Where(x => x.Status != IncidentStatus.Resolved && x.Severity == Severity.Critical)
            .ToArray();

        if (openCritical.Length == 0)
            return;

        blockers.Add($"{openCritical.Length} critical incident(s) remain unresolved.");
        actions.Add("Resolve or explicitly mitigate all critical incidents with fresh evidence.");
    }

    private async Task EvaluateDurableJobsAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> warnings,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var jobs = await _jobs.ListRecentAsync(request.ProjectId, 100, ct);
        var failedJobs = jobs.Count(x =>
            x.Status is DurableJobStatus.Failed or DurableJobStatus.TimedOut);
        var runningJobs = jobs.Count(x =>
            x.Status is DurableJobStatus.Running or DurableJobStatus.Queued);

        if (failedJobs > 0)
        {
            warnings.Add($"{failedJobs} recent durable job(s) failed or timed out.");
            actions.Add("Review failed durable jobs and rerun release-relevant checks.");
        }

        if (runningJobs > 0)
            warnings.Add($"{runningJobs} durable job(s) are still queued/running.");
    }

    private async Task<IReadOnlyList<PerformanceBudgetEvaluation>> EvaluatePerformanceBudgetsAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> blockers,
        ICollection<string> warnings,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var evaluations = await _budgets.ListEvaluationsAsync(request.ProjectId, 50, ct);

        if (evaluations.Any(x => x.Verdict == PerformanceBudgetVerdict.Failed))
        {
            blockers.Add("At least one recent platform performance budget evaluation failed.");
            actions.Add(
                "Restore the failed platform performance budget and rerun the affected benchmark/replay.");
        }
        else if (evaluations.Any(x => x.Verdict == PerformanceBudgetVerdict.Warning))
        {
            warnings.Add("A recent performance budget is near its configured boundary.");
        }

        return evaluations;
    }

    private async Task<IReadOnlyList<EvidenceRecord>> EvaluateFreshEvidenceAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> blockers,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var freshnessDays = Math.Clamp(request.EvidenceFreshnessDays, 1, 90);
        var evidence = await _evidence.ListForProjectAsync(
            request.ProjectId,
            _clock.UtcNow.AddDays(-freshnessDays),
            ct);

        if (evidence.Count == 0)
        {
            blockers.Add("No fresh release evidence exists in the configured freshness window.");
            actions.Add("Run the release QA/benchmark/replay suite to generate fresh evidence.");
        }

        return evidence;
    }

    private async Task EvaluateQaGovernanceAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> blockers,
        ICollection<string> warnings,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var activePolicy = await _qaGovernance.GetActivePolicyAsync(request.ProjectId, ct);
        var policyValid = TryParseQaPolicyRules(activePolicy, out var rules);

        if (activePolicy is not null && !policyValid)
        {
            blockers.Add("The active QA policy contains invalid JSON and cannot be trusted.");
            actions.Add("Repair the active QA policy and rerun QA governance before release.");
        }

        var gate = await _qaGovernance.GetLatestPolicyEvaluationAsync(request.ProjectId, ct);
        var overrides = await _qaGovernance.ListReleaseOverridesAsync(request.ProjectId, 100, ct);
        var effectiveOverride = gate is null
            ? null
            : overrides.FirstOrDefault(x =>
                x.IsEffective(_clock.UtcNow)
                && x.PolicyEvaluationId == gate.Id
                && string.Equals(x.CommitSha, request.CommitSha, StringComparison.OrdinalIgnoreCase));

        if (gate is null)
        {
            warnings.Add("No QA governance policy evaluation exists for this project.");
            actions.Add("Run QA with the governance pipeline before release.");
        }
        else if (gate.Verdict == QaGateVerdict.NoGo && effectiveOverride is null)
        {
            blockers.Add($"QA policy gate is NO-GO: {gate.Summary}");
            actions.Add(
                "Resolve the blockers or obtain an owner-approved, commit-bound, unexpired release override.");
        }
        else if (gate.Verdict == QaGateVerdict.NoGo && effectiveOverride is not null)
        {
            warnings.Add(
                $"QA NO-GO is temporarily overridden by {effectiveOverride.ApprovedBy}; "
                + $"override expires {effectiveOverride.ExpiresAtUtc:O}. Original gate: {gate.Summary}");
            actions.Add(
                "Review the accepted risk before the override expires and resolve the underlying QA blocker.");
        }
        else if (gate.Verdict == QaGateVerdict.ReviewRequired)
        {
            warnings.Add($"QA policy gate requires review: {gate.Summary}");
        }
        else if (gate.Verdict == QaGateVerdict.Unavailable)
        {
            blockers.Add("QA policy gate is unavailable; release quality cannot be verified.");
        }

        var maxAgeDays = Math.Clamp(
            activePolicy is null ? request.EvidenceFreshnessDays : rules.MaxEvidenceAgeDays,
            1,
            90);

        var provenance = await _qaGovernance.GetLatestProvenanceAsync(request.ProjectId, ct);
        if (provenance is null)
        {
            blockers.Add("No QA provenance exists for the release candidate.");
            actions.Add("Run QA on the candidate commit to capture provenance.");
            return;
        }

        var age = (_clock.UtcNow - provenance.CapturedAtUtc).TotalDays;
        if (age > maxAgeDays)
        {
            var message =
                $"Latest QA provenance is stale ({age:0.#} days old; policy maximum {maxAgeDays} days).";

            if (activePolicy is null || !policyValid || rules.BlockOnStaleEvidence)
                blockers.Add(message);
            else
                warnings.Add(message);

            actions.Add("Rerun QA on the release candidate to refresh exact-revision evidence.");
        }

        if (string.IsNullOrWhiteSpace(provenance.CommitSha))
        {
            blockers.Add("Latest QA provenance does not identify an exact commit.");
            actions.Add("Run QA against an immutable release-candidate commit SHA.");
        }
        else if (!string.Equals(
                     provenance.CommitSha,
                     request.CommitSha,
                     StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(
                "Latest QA provenance belongs to a different commit than the release candidate.");
            actions.Add("Run QA against the exact candidate commit.");
        }
    }

    private async Task<ProjectTruthSnapshot?> EvaluateProjectTruthAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> blockers,
        ICollection<string> warnings,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var truth = await _truth.GetLatestAsync(request.ProjectId, ct);
        if (truth is null)
        {
            warnings.Add("Project Truth has not been evaluated yet.");
            return null;
        }

        if (truth.BlockerCount > 0)
        {
            blockers.Add($"Project Truth reports {truth.BlockerCount} blocker(s).");
            actions.Add("Resolve Project Truth blockers before release.");
        }

        if (truth.UnverifiedClaimCount > 0)
            warnings.Add($"{truth.UnverifiedClaimCount} project claim(s) remain unverified.");

        return truth;
    }

    private async Task EvaluateReleaseEvidenceAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> warnings,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var bundle = await _releaseEvidence.GetLatestAsync(request.ProjectId, ct);
        if (bundle is not null
            && string.Equals(bundle.CommitSha, request.CommitSha, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        warnings.Add("No release provenance/SBOM bundle is attached to this exact commit.");
        actions.Add("Generate a release evidence bundle for the candidate commit.");
    }

    private async Task EvaluateChangeRiskAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> blockers,
        ICollection<string> warnings,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var risk = await _risk.GetLatestAsync(request.ProjectId, request.CommitSha, ct);
        if (risk is null)
            return;

        if (risk.RiskLevel == ChangeRiskLevel.Critical)
        {
            blockers.Add(
                $"Change risk for {request.CommitSha} is Critical ({risk.RiskScore:0.#}/100).");
            actions.Add("Run the expanded high-risk QA plan and reassess change risk.");
        }
        else if (risk.RiskLevel == ChangeRiskLevel.High)
        {
            warnings.Add($"Change risk is High ({risk.RiskScore:0.#}/100).");
        }
    }

    private async Task EvaluateDevelopmentGraphAsync(
        EvaluateReleaseReadinessRequest request,
        ICollection<string> blockers,
        ICollection<string> warnings,
        ICollection<string> actions,
        CancellationToken ct)
    {
        var nodes = await _graph.ListNodesAsync(request.ProjectId, null, ct);

        var structuredTypes = new HashSet<DevelopmentGraphNodeType>
        {
            DevelopmentGraphNodeType.Blocker,
            DevelopmentGraphNodeType.Requirement,
            DevelopmentGraphNodeType.Risk,
            DevelopmentGraphNodeType.ActionItem,
            DevelopmentGraphNodeType.ChangeRequest,
            DevelopmentGraphNodeType.AcceptanceCriterion
        };

        var malformed = nodes
            .Where(x => structuredTypes.Contains(x.Type))
            .Count(x => !HasValidObjectMetadata(x.MetadataJson));

        if (malformed > 0)
        {
            blockers.Add(
                $"{malformed} release-relevant Development Graph node(s) contain malformed metadata.");
            actions.Add(
                "Repair malformed Development Graph metadata and regenerate the affected evidence before release.");
        }

        var openManagementBlockers = nodes
            .Where(x =>
                x.Type == DevelopmentGraphNodeType.Blocker
                && !IsResolvedManagementStatus(x.Status))
            .ToArray();

        var criticalManagementBlockers =
            openManagementBlockers.Count(IsCriticalManagementBlocker);

        if (criticalManagementBlockers > 0)
        {
            blockers.Add(
                $"{criticalManagementBlockers} critical management blocker(s) remain open.");
            actions.Add(
                "Resolve or explicitly mitigate critical schedule/workstream blockers before release.");
        }
        else if (openManagementBlockers.Length > 0)
        {
            warnings.Add(
                $"{openManagementBlockers.Length} management blocker(s) remain open.");
        }

        var incompleteHighPriorityRequirements = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.Requirement
            && IsHighPriorityRequirementOpen(x));

        if (incompleteHighPriorityRequirements > 0)
        {
            warnings.Add(
                $"{incompleteHighPriorityRequirements} high-priority in-scope requirement(s) are incomplete.");
            actions.Add(
                "Confirm incomplete high-priority requirements are either completed or explicitly removed/deferred from release scope.");
        }

        var openManagementRisks = nodes
            .Where(x =>
                x.Type == DevelopmentGraphNodeType.Risk
                && !IsResolvedManagementStatus(x.Status))
            .ToArray();

        var criticalManagementRisks =
            openManagementRisks.Count(x => ManagementRiskExposure(x) >= 70m);
        var highManagementRisks =
            openManagementRisks.Count(x =>
                ManagementRiskExposure(x) >= 45m
                && ManagementRiskExposure(x) < 70m);

        if (criticalManagementRisks > 0)
        {
            blockers.Add(
                $"{criticalManagementRisks} critical management risk(s) remain open.");
            actions.Add(
                "Review mitigation, ownership, and release impact for every critical management risk.");
        }
        else if (highManagementRisks > 0)
        {
            warnings.Add($"{highManagementRisks} high management risk(s) remain open.");
        }

        var overdueActions = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.ActionItem
            && !IsResolvedManagementStatus(x.Status)
            && IsPastDue(x, "DueAtUtc", _clock.UtcNow));

        if (overdueActions > 0)
        {
            warnings.Add($"{overdueActions} management action item(s) are overdue.");
            actions.Add(
                "Escalate overdue release-relevant actions or explicitly re-plan their due dates.");
        }

        var unresolvedScopeChanges = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.ChangeRequest
            && !IsFinalChangeStatus(x.Status)
            && IsMaterialScopeChange(x));

        if (unresolvedScopeChanges > 0)
        {
            warnings.Add(
                $"{unresolvedScopeChanges} material scope change request(s) are unresolved.");
            actions.Add(
                "Approve, reject, or defer material scope changes before committing the final release scope/date.");
        }

        AddSimpleStatusChecks(nodes, blockers, warnings, actions);
    }

    private static void AddSimpleStatusChecks(
        IReadOnlyList<DevelopmentGraphNode> nodes,
        ICollection<string> blockers,
        ICollection<string> warnings,
        ICollection<string> actions)
    {
        var blockedMilestones = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.Milestone && IsBlockingStatus(x.Status));

        if (blockedMilestones > 0)
        {
            blockers.Add(
                $"{blockedMilestones} milestone(s) are blocked/at risk on the Development Graph.");
            actions.Add(
                "Resolve critical-path milestone blockers or explicitly rescope the release.");
        }

        var notReadyFeatures = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.Feature && IsBlockingStatus(x.Status));

        if (notReadyFeatures > 0)
        {
            blockers.Add($"{notReadyFeatures} feature(s) are not release-ready.");
            actions.Add(
                "Complete required feature acceptance criteria or remove the features from release scope.");
        }

        var failedCriteria = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.AcceptanceCriterion
            && IsCriterionBlocking(x));

        if (failedCriteria > 0)
        {
            blockers.Add(
                $"{failedCriteria} required acceptance criterion/criteria lack passing evidence.");
            actions.Add("Attach fresh evidence for all required acceptance criteria.");
        }

        var blockedReviews = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.Review
            && (StatusEquals(x.Status, "Blocked") || StatusEquals(x.Status, "HighRisk")));

        if (blockedReviews > 0)
        {
            warnings.Add(
                $"{blockedReviews} review assessment(s) require attention before release.");
            actions.Add(
                "Resolve blocked/high-risk reviews and rerun change-impact analysis.");
        }

        var criticalAssets = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.Asset && StatusEquals(x.Status, "Critical"));

        if (criticalAssets > 0)
        {
            blockers.Add($"{criticalAssets} critical asset issue(s) remain open.");
            actions.Add("Resolve asset import/reference/platform compatibility blockers.");
        }

        var failedCi = nodes.Count(x =>
            x.Type == DevelopmentGraphNodeType.CiRun && IsFailedStatus(x.Status));

        if (failedCi > 0)
        {
            blockers.Add($"{failedCi} observed CI run(s) are failed/error state.");
            actions.Add(
                "Restore green CI for the candidate revision or document an explicit exception with evidence.");
        }
    }

    private static bool IsResolvedManagementStatus(string status) =>
        ResolvedManagementStatuses.Any(x => StatusEquals(status, x));

    private static bool IsCriticalManagementBlocker(DevelopmentGraphNode node)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;

        return root.TryGetProperty("Severity", out var severity)
               && StatusEquals(severity.GetString() ?? string.Empty, "Critical");
    }

    private static bool IsHighPriorityRequirementOpen(DevelopmentGraphNode node)
    {
        if (IsResolvedManagementStatus(node.Status))
            return false;

        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;

        var priority = root.TryGetProperty("Priority", out var priorityElement)
            ? priorityElement.GetString() ?? "Medium"
            : "Medium";

        var scope = root.TryGetProperty("ScopeState", out var scopeElement)
            ? scopeElement.GetString() ?? "Included"
            : "Included";

        return (StatusEquals(priority, "Critical") || StatusEquals(priority, "High"))
               && !StatusEquals(scope, "Deferred")
               && !StatusEquals(scope, "Removed");
    }

    private static decimal ManagementRiskExposure(DevelopmentGraphNode node)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return 100m;

        var probability = root.TryGetProperty("ProbabilityPercent", out var probabilityElement)
                          && probabilityElement.TryGetDecimal(out var probabilityValue)
            ? probabilityValue
            : 0m;

        var impact = root.TryGetProperty("ImpactPercent", out var impactElement)
                     && impactElement.TryGetDecimal(out var impactValue)
            ? impactValue
            : 0m;

        return Math.Clamp(probability, 0m, 100m)
               * Math.Clamp(impact, 0m, 100m)
               / 100m;
    }

    private static bool IsPastDue(
        DevelopmentGraphNode node,
        string property,
        DateTimeOffset now)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;

        return root.TryGetProperty(property, out var value)
               && value.TryGetDateTimeOffset(out var due)
               && due < now;
    }

    private static bool IsFinalChangeStatus(string status) =>
        StatusEquals(status, "Approved")
        || StatusEquals(status, "Rejected")
        || StatusEquals(status, "Cancelled")
        || StatusEquals(status, "Closed");

    private static bool IsMaterialScopeChange(DevelopmentGraphNode node)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;

        var days = root.TryGetProperty("ScheduleDaysDelta", out var daysElement)
                   && daysElement.TryGetInt32(out var daysValue)
            ? Math.Abs(daysValue)
            : 0;

        var risk = root.TryGetProperty("RiskDeltaPercent", out var riskElement)
                   && riskElement.TryGetDecimal(out var riskValue)
            ? Math.Abs(riskValue)
            : 0m;

        var engineering = root.TryGetProperty("EngineeringHoursDelta", out var engineeringElement)
                          && engineeringElement.TryGetDecimal(out var engineeringValue)
            ? Math.Abs(engineeringValue)
            : 0m;

        return days > 0 || risk >= 10m || engineering >= 40m;
    }

    private static bool IsBlockingStatus(string status) =>
        BlockingStatuses.Any(x => StatusEquals(status, x));

    private static bool IsFailedStatus(string status) =>
        FailedStatuses.Any(x => StatusEquals(status, x));

    private static bool IsCriterionBlocking(DevelopmentGraphNode node)
    {
        var statusRequiresEvidence =
            StatusEquals(node.Status, "Failed")
            || StatusEquals(node.Status, "Unverified")
            || StatusEquals(node.Status, "MissingEvidence");

        if (!statusRequiresEvidence)
            return false;

        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;

        return !root.TryGetProperty("Required", out var required)
               || required.ValueKind != JsonValueKind.False;
    }

    private static bool TryParseQaPolicyRules(QaPolicy? policy, out QaPolicyRules rules)
    {
        if (policy is null)
        {
            rules = new QaPolicyRules();
            return true;
        }

        try
        {
            rules = JsonSerializer.Deserialize<QaPolicyRules>(policy.RulesJson)
                    ?? new QaPolicyRules();
            return true;
        }
        catch (JsonException)
        {
            rules = new QaPolicyRules();
            return false;
        }
    }

    private static bool HasValidObjectMetadata(string json) =>
        TryGetObjectMetadata(json, out _);

    private static bool TryGetObjectMetadata(string json, out JsonElement root)
    {
        root = default;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool StatusEquals(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static ReleaseReadinessDto Map(ReleaseReadinessSnapshot snapshot) =>
        new(
            snapshot.Id,
            snapshot.CommitSha,
            snapshot.Decision.ToString(),
            snapshot.Confidence,
            JsonSerializer.Deserialize<string[]>(snapshot.BlockersJson) ?? [],
            JsonSerializer.Deserialize<string[]>(snapshot.WarningsJson) ?? [],
            JsonSerializer.Deserialize<string[]>(snapshot.RequiredActionsJson) ?? [],
            snapshot.Summary,
            snapshot.EvaluatedAtUtc);
}
