using Dip.Application.Features.Dashboard;
using Dip.Application.Features.GitHubIntegration;

namespace Dip.Web.Services;

public enum ProjectOperationStepState
{
    Completed,
    Current,
    Attention,
    Pending,
    Unavailable
}

public sealed record ProjectOperationStep(
    int Number,
    string Key,
    string Title,
    string Description,
    ProjectOperationStepState State);

public sealed record ProjectOperationFlowModel(
    IReadOnlyList<ProjectOperationStep> Steps,
    string CurrentState,
    string Why,
    string NextActionLabel,
    string NextActionHref,
    bool AttentionRequired,
    string StageKey);

/// <summary>
/// Resolves the project-level operation flow only from evidence already exposed by
/// the dashboard and repository read models. It deliberately does not use AI,
/// hidden heuristics, or invented runtime state.
/// </summary>
public static class ProjectOperationFlowResolver
{
    private static readonly (string Key, string Title, string Description)[] Definitions =
    [
        ("connect", "Connect", "Attach the source repository and synchronize the workspace revision."),
        ("collect", "Collect", "Collect repository, CI, QA and runtime evidence for the project."),
        ("verify", "Verify", "Run QA against the synchronized workspace and keep verification current."),
        ("understand", "Understand", "Evaluate Project Truth from the evidence available to DIP."),
        ("decide", "Decide", "Review blockers, contradictions and quality gates before acting."),
        ("monitor", "Monitor", "Keep evidence current as repository, CI and QA state changes.")
    ];

    public static ProjectOperationFlowModel Resolve(
        Guid projectId,
        ProjectDashboardDetails dashboard,
        GitHubProjectRepositoryDto? repository,
        string? repositoryError = null)
    {
        ArgumentNullException.ThrowIfNull(dashboard);

        var repositoryHref = $"/dashboard/{projectId}/repository";
        var qaHref = $"/dashboard/{projectId}/qa?autorun=true";
        var intelligenceHref = $"/dashboard/{projectId}/intelligence";
        var attentionHref = $"/dashboard/{projectId}#needs-attention";
        var ciHref = $"/dashboard/{projectId}/ci";

        var connected = repository?.Connection.Connected == true;
        var synchronized = repository?.Workspace is not null;
        var latestQa = dashboard.RecentQaRuns
            .OrderByDescending(run => run.CreatedAtUtc)
            .FirstOrDefault();
        var qaPredatesWorkspace = latestQa is not null
            && repository?.Workspace is { } workspace
            && latestQa.CreatedAtUtc < workspace.SyncedAtUtc;

        var hasEvidence = dashboard.Timeline.Count > 0
            || dashboard.LatestPipelineRunId.HasValue
            || dashboard.RecentQaRuns.Count > 0
            || dashboard.Metrics.Count > 0
            || dashboard.TruthEvaluatedAtUtc.HasValue;

        if (!string.IsNullOrWhiteSpace(repositoryError))
        {
            return Build(
                currentIndex: 0,
                currentStepState: ProjectOperationStepState.Unavailable,
                currentState: "Repository state unavailable",
                why: $"Repository status could not be loaded: {repositoryError}",
                nextActionLabel: "Open repository diagnostics",
                nextActionHref: repositoryHref);
        }

        if (!connected)
        {
            return Build(
                currentIndex: 0,
                currentStepState: ProjectOperationStepState.Attention,
                currentState: "Source connection required",
                why: repository?.Warning
                    ?? "DIP cannot confirm a connected source repository for this project.",
                nextActionLabel: "Open repository setup",
                nextActionHref: repositoryHref);
        }

        if (!synchronized)
        {
            return Build(
                currentIndex: 0,
                currentStepState: ProjectOperationStepState.Current,
                currentState: "Repository synchronization required",
                why: repository?.Warning
                    ?? "Repository access is connected, but no synchronized workspace revision is available for QA.",
                nextActionLabel: "Synchronize repository",
                nextActionHref: repositoryHref);
        }

        if (!hasEvidence)
        {
            return Build(
                currentIndex: 1,
                currentStepState: ProjectOperationStepState.Current,
                currentState: "Evidence collection required",
                why: "The workspace is synchronized, but DIP has not received project evidence yet.",
                nextActionLabel: "Run QA and collect evidence",
                nextActionHref: qaHref);
        }

        if (latestQa is null)
        {
            return Build(
                currentIndex: 2,
                currentStepState: ProjectOperationStepState.Current,
                currentState: "Verification required",
                why: "Evidence exists, but no QA run is available for the project.",
                nextActionLabel: "Run QA",
                nextActionHref: qaHref);
        }

        if (qaPredatesWorkspace)
        {
            return Build(
                currentIndex: 2,
                currentStepState: ProjectOperationStepState.Attention,
                currentState: "Verification refresh required",
                why: "The latest QA evidence predates the most recent repository workspace synchronization.",
                nextActionLabel: "Verify latest workspace",
                nextActionHref: qaHref);
        }

        if (!dashboard.TruthEvaluatedAtUtc.HasValue)
        {
            return Build(
                currentIndex: 3,
                currentStepState: ProjectOperationStepState.Current,
                currentState: "Project Truth evaluation required",
                why: "Verification evidence is available, but the dashboard has no Project Truth evaluation timestamp.",
                nextActionLabel: "Open Development Intelligence",
                nextActionHref: intelligenceHref);
        }

        if (dashboard.BlockerCount > 0 || dashboard.Blockers.Count > 0)
        {
            return Build(
                currentIndex: 4,
                currentStepState: ProjectOperationStepState.Attention,
                currentState: "Blocking action required",
                why: $"Current evidence identifies {Math.Max(dashboard.BlockerCount, dashboard.Blockers.Count)} active blocker(s).",
                nextActionLabel: "Review blockers",
                nextActionHref: attentionHref);
        }

        if (dashboard.ContradictionCount > 0)
        {
            return Build(
                currentIndex: 4,
                currentStepState: ProjectOperationStepState.Attention,
                currentState: "Evidence reconciliation required",
                why: $"DIP reports {dashboard.ContradictionCount} contradiction(s) between project claims and evidence.",
                nextActionLabel: "Inspect contradictions",
                nextActionHref: intelligenceHref);
        }

        if (QualityGateNeedsAttention(dashboard.QualityGateStatus) || dashboard.CriticalIssueCount > 0)
        {
            return Build(
                currentIndex: 4,
                currentStepState: ProjectOperationStepState.Attention,
                currentState: "Quality Gate review required",
                why: dashboard.CriticalIssueCount > 0
                    ? $"The latest project view contains {dashboard.CriticalIssueCount} critical quality issue(s)."
                    : $"The latest Quality Gate state is {dashboard.QualityGateStatus}.",
                nextActionLabel: "Open QA evidence",
                nextActionHref: qaHref);
        }

        if (TruthNeedsAttention(dashboard.TruthStatus))
        {
            return Build(
                currentIndex: 4,
                currentStepState: ProjectOperationStepState.Attention,
                currentState: "Project decision required",
                why: $"Project Truth currently reports {dashboard.TruthStatus}. {dashboard.TruthSummary}",
                nextActionLabel: "Review recommended actions",
                nextActionHref: attentionHref);
        }

        return Build(
            currentIndex: 5,
            currentStepState: ProjectOperationStepState.Current,
            currentState: "Continuous monitoring",
            why: "The source is synchronized, QA evidence is current relative to the workspace, and no blocking signal is present.",
            nextActionLabel: "Inspect CI / builds",
            nextActionHref: ciHref);
    }

    private static ProjectOperationFlowModel Build(
        int currentIndex,
        ProjectOperationStepState currentStepState,
        string currentState,
        string why,
        string nextActionLabel,
        string nextActionHref)
    {
        var steps = Definitions
            .Select((definition, index) => new ProjectOperationStep(
                index + 1,
                definition.Key,
                definition.Title,
                definition.Description,
                ResolveStepState(index, currentIndex, currentStepState)))
            .ToArray();

        return new ProjectOperationFlowModel(
            steps,
            currentState,
            why,
            nextActionLabel,
            nextActionHref,
            currentStepState is ProjectOperationStepState.Attention or ProjectOperationStepState.Unavailable,
            Definitions[currentIndex].Key);
    }

    private static ProjectOperationStepState ResolveStepState(
        int index,
        int currentIndex,
        ProjectOperationStepState currentStepState)
    {
        if (index < currentIndex)
            return ProjectOperationStepState.Completed;
        if (index > currentIndex)
            return ProjectOperationStepState.Pending;
        return currentStepState;
    }

    private static bool QualityGateNeedsAttention(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return status.Trim().ToLowerInvariant() switch
        {
            "failed" or "failure" or "blocked" or "error" or "critical" or "rejected" => true,
            _ => false
        };
    }

    private static bool TruthNeedsAttention(string status)
        => status.Trim().ToLowerInvariant() is "attention" or "atrisk" or "at-risk" or "blocked";
}
