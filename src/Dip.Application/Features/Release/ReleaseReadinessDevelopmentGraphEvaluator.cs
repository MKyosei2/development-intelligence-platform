// Purpose: Evaluates release-relevant Development Graph metadata, blockers, requirements, risks, actions, changes, CI, reviews, and assets.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;

internal sealed class ReleaseReadinessDevelopmentGraphEvaluator(
    IDevelopmentGraphRepository graph,
    ISystemClock clock)
{
    private static readonly string[] ResolvedManagementStatuses = ["Resolved", "Closed", "Done", "Completed"];
    private static readonly string[] BlockingStatuses = ["Blocked", "AtRisk", "NotReady", "Failed", "Critical"];
    private static readonly string[] FailedStatuses = ["Failed", "Failure", "Error", "Cancelled"];

    public async Task EvaluateAsync(ReleaseReadinessEvaluationContext context, CancellationToken ct)
    {
        var nodes = await graph.ListNodesAsync(context.Request.ProjectId, null, ct);
        var structuredTypes = new HashSet<DevelopmentGraphNodeType>
        {
            DevelopmentGraphNodeType.Blocker,
            DevelopmentGraphNodeType.Requirement,
            DevelopmentGraphNodeType.Risk,
            DevelopmentGraphNodeType.ActionItem,
            DevelopmentGraphNodeType.ChangeRequest,
            DevelopmentGraphNodeType.AcceptanceCriterion
        };

        var malformed = nodes.Where(x => structuredTypes.Contains(x.Type)).Count(x => !HasValidObjectMetadata(x.MetadataJson));
        if (malformed > 0)
        {
            context.Blockers.Add($"{malformed} release-relevant Development Graph node(s) contain malformed metadata.");
            context.Actions.Add("Repair malformed Development Graph metadata and regenerate the affected evidence before release.");
        }

        var openManagementBlockers = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Blocker && !IsResolvedManagementStatus(x.Status)).ToArray();
        var criticalManagementBlockers = openManagementBlockers.Count(IsCriticalManagementBlocker);
        if (criticalManagementBlockers > 0)
        {
            context.Blockers.Add($"{criticalManagementBlockers} critical management blocker(s) remain open.");
            context.Actions.Add("Resolve or explicitly mitigate critical schedule/workstream blockers before release.");
        }
        else if (openManagementBlockers.Length > 0)
        {
            context.Warnings.Add($"{openManagementBlockers.Length} management blocker(s) remain open.");
        }

        var incompleteHighPriorityRequirements = nodes.Count(x => x.Type == DevelopmentGraphNodeType.Requirement && IsHighPriorityRequirementOpen(x));
        if (incompleteHighPriorityRequirements > 0)
        {
            context.Warnings.Add($"{incompleteHighPriorityRequirements} high-priority in-scope requirement(s) are incomplete.");
            context.Actions.Add("Confirm incomplete high-priority requirements are either completed or explicitly removed/deferred from release scope.");
        }

        var openManagementRisks = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Risk && !IsResolvedManagementStatus(x.Status)).ToArray();
        var criticalManagementRisks = openManagementRisks.Count(x => ManagementRiskExposure(x) >= 70m);
        var highManagementRisks = openManagementRisks.Count(x => ManagementRiskExposure(x) >= 45m && ManagementRiskExposure(x) < 70m);
        if (criticalManagementRisks > 0)
        {
            context.Blockers.Add($"{criticalManagementRisks} critical management risk(s) remain open.");
            context.Actions.Add("Review mitigation, ownership, and release impact for every critical management risk.");
        }
        else if (highManagementRisks > 0)
        {
            context.Warnings.Add($"{highManagementRisks} high management risk(s) remain open.");
        }

        var overdueActions = nodes.Count(x => x.Type == DevelopmentGraphNodeType.ActionItem && !IsResolvedManagementStatus(x.Status) && IsPastDue(x, "DueAtUtc", clock.UtcNow));
        if (overdueActions > 0)
        {
            context.Warnings.Add($"{overdueActions} management action item(s) are overdue.");
            context.Actions.Add("Escalate overdue release-relevant actions or explicitly re-plan their due dates.");
        }

        var unresolvedScopeChanges = nodes.Count(x => x.Type == DevelopmentGraphNodeType.ChangeRequest && !IsFinalChangeStatus(x.Status) && IsMaterialScopeChange(x));
        if (unresolvedScopeChanges > 0)
        {
            context.Warnings.Add($"{unresolvedScopeChanges} material scope change request(s) are unresolved.");
            context.Actions.Add("Approve, reject, or defer material scope changes before committing the final release scope/date.");
        }

        AddSimpleStatusChecks(nodes, context);
    }

    private static void AddSimpleStatusChecks(IReadOnlyList<DevelopmentGraphNode> nodes, ReleaseReadinessEvaluationContext context)
    {
        var blockedMilestones = nodes.Count(x => x.Type == DevelopmentGraphNodeType.Milestone && IsBlockingStatus(x.Status));
        if (blockedMilestones > 0)
        {
            context.Blockers.Add($"{blockedMilestones} milestone(s) are blocked/at risk on the Development Graph.");
            context.Actions.Add("Resolve critical-path milestone blockers or explicitly rescope the release.");
        }
        var notReadyFeatures = nodes.Count(x => x.Type == DevelopmentGraphNodeType.Feature && IsBlockingStatus(x.Status));
        if (notReadyFeatures > 0)
        {
            context.Blockers.Add($"{notReadyFeatures} feature(s) are not release-ready.");
            context.Actions.Add("Complete required feature acceptance criteria or remove the features from release scope.");
        }
        var failedCriteria = nodes.Count(x => x.Type == DevelopmentGraphNodeType.AcceptanceCriterion && IsCriterionBlocking(x));
        if (failedCriteria > 0)
        {
            context.Blockers.Add($"{failedCriteria} required acceptance criterion/criteria lack passing evidence.");
            context.Actions.Add("Attach fresh evidence for all required acceptance criteria.");
        }
        var blockedReviews = nodes.Count(x => x.Type == DevelopmentGraphNodeType.Review && (StatusEquals(x.Status, "Blocked") || StatusEquals(x.Status, "HighRisk")));
        if (blockedReviews > 0)
        {
            context.Warnings.Add($"{blockedReviews} review assessment(s) require attention before release.");
            context.Actions.Add("Resolve blocked/high-risk reviews and rerun change-impact analysis.");
        }
        var criticalAssets = nodes.Count(x => x.Type == DevelopmentGraphNodeType.Asset && StatusEquals(x.Status, "Critical"));
        if (criticalAssets > 0)
        {
            context.Blockers.Add($"{criticalAssets} critical asset issue(s) remain open.");
            context.Actions.Add("Resolve asset import/reference/platform compatibility blockers.");
        }
        var failedCi = nodes.Count(x => x.Type == DevelopmentGraphNodeType.CiRun && IsFailedStatus(x.Status));
        if (failedCi > 0)
        {
            context.Blockers.Add($"{failedCi} observed CI run(s) are failed/error state.");
            context.Actions.Add("Restore green CI for the candidate revision or document an explicit exception with evidence.");
        }
    }

    private static bool IsResolvedManagementStatus(string status) => ResolvedManagementStatuses.Any(x => StatusEquals(status, x));

    private static bool IsCriticalManagementBlocker(DevelopmentGraphNode node)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;
        return root.TryGetProperty("Severity", out var severity) && StatusEquals(severity.GetString() ?? string.Empty, "Critical");
    }

    private static bool IsHighPriorityRequirementOpen(DevelopmentGraphNode node)
    {
        if (IsResolvedManagementStatus(node.Status))
            return false;
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;
        var priority = root.TryGetProperty("Priority", out var priorityElement) ? priorityElement.GetString() ?? "Medium" : "Medium";
        var scope = root.TryGetProperty("ScopeState", out var scopeElement) ? scopeElement.GetString() ?? "Included" : "Included";
        return (StatusEquals(priority, "Critical") || StatusEquals(priority, "High")) && !StatusEquals(scope, "Deferred") && !StatusEquals(scope, "Removed");
    }

    private static decimal ManagementRiskExposure(DevelopmentGraphNode node)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return 100m;
        var probability = root.TryGetProperty("ProbabilityPercent", out var p) && p.TryGetDecimal(out var probabilityValue) ? probabilityValue : 0m;
        var impact = root.TryGetProperty("ImpactPercent", out var i) && i.TryGetDecimal(out var impactValue) ? impactValue : 0m;
        return Math.Clamp(probability, 0m, 100m) * Math.Clamp(impact, 0m, 100m) / 100m;
    }

    private static bool IsPastDue(DevelopmentGraphNode node, string property, DateTimeOffset now)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;
        return root.TryGetProperty(property, out var value) && value.TryGetDateTimeOffset(out var due) && due < now;
    }

    private static bool IsFinalChangeStatus(string status) =>
        StatusEquals(status, "Approved") || StatusEquals(status, "Rejected") || StatusEquals(status, "Cancelled") || StatusEquals(status, "Closed");

    private static bool IsMaterialScopeChange(DevelopmentGraphNode node)
    {
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;
        var days = root.TryGetProperty("ScheduleDaysDelta", out var d) && d.TryGetInt32(out var daysValue) ? Math.Abs(daysValue) : 0;
        var risk = root.TryGetProperty("RiskDeltaPercent", out var r) && r.TryGetDecimal(out var riskValue) ? Math.Abs(riskValue) : 0m;
        var engineering = root.TryGetProperty("EngineeringHoursDelta", out var e) && e.TryGetDecimal(out var engineeringValue) ? Math.Abs(engineeringValue) : 0m;
        return days > 0 || risk >= 10m || engineering >= 40m;
    }

    private static bool IsBlockingStatus(string status) => BlockingStatuses.Any(x => StatusEquals(status, x));
    private static bool IsFailedStatus(string status) => FailedStatuses.Any(x => StatusEquals(status, x));

    private static bool IsCriterionBlocking(DevelopmentGraphNode node)
    {
        var statusRequiresEvidence = StatusEquals(node.Status, "Failed") || StatusEquals(node.Status, "Unverified") || StatusEquals(node.Status, "MissingEvidence");
        if (!statusRequiresEvidence)
            return false;
        if (!TryGetObjectMetadata(node.MetadataJson, out var root))
            return true;
        return !root.TryGetProperty("Required", out var required) || required.ValueKind != JsonValueKind.False;
    }

    private static bool HasValidObjectMetadata(string json) => TryGetObjectMetadata(json, out _);

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

    private static bool StatusEquals(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
