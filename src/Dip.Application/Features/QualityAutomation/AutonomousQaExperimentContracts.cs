// Purpose: Defines executable autonomous-QA experiment plans, evidence requirements, results, and the dispatcher boundary.
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public enum AutonomousQaExperimentPhase
{
    Baseline,
    PostPatch
}

public enum AutonomousQaExperimentStatus
{
    Planned,
    Passed,
    Warning,
    Failed,
    Unavailable,
    NotConfigured,
    InfrastructureFailure,
    Skipped
}

public sealed record AutonomousQaRequiredEvidence(
    string Kind,
    string Description,
    bool Required = true);

public sealed record AutonomousQaExperimentStep(
    string Id,
    DebugExperimentKind Kind,
    bool Required,
    string Reason,
    string SuccessCondition,
    string FalsificationCondition,
    IReadOnlyList<AutonomousQaRequiredEvidence> RequiredEvidence,
    string? ConfigurationJson = null,
    int TimeoutSeconds = 900);

public sealed record AutonomousQaExperimentPlan(
    Guid PlanId,
    string HypothesisId,
    string SubjectKey,
    string? CommitSha,
    IReadOnlyList<AutonomousQaExperimentStep> Steps);

public sealed record AutonomousQaExperimentExecutionContext(
    Guid ProjectId,
    string SubjectKey,
    string WorkspaceRelativePath,
    string? CommitSha,
    LanguageId Language,
    AutonomousQaExperimentPhase Phase,
    string? ExperimentConfigurationJson = null,
    string? WorkspaceOverridePath = null);

public sealed record AutonomousQaExperimentResult(
    string StepId,
    DebugExperimentKind Kind,
    AutonomousQaExperimentPhase Phase,
    AutonomousQaExperimentStatus Status,
    bool Verified,
    string Summary,
    IReadOnlyList<string> EvidenceReferences,
    string? ArtifactReference,
    string? EnvironmentFingerprint,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public interface IAutonomousQaExperimentDispatcher
{
    Task<AutonomousQaExperimentResult> ExecuteAsync(
        AutonomousQaExperimentStep step,
        AutonomousQaExperimentExecutionContext context,
        CancellationToken ct);

    Task<IReadOnlyList<AutonomousQaExperimentResult>> ExecutePlanAsync(
        AutonomousQaExperimentPlan plan,
        AutonomousQaExperimentExecutionContext context,
        CancellationToken ct);
}

/// <summary>
/// Central policy for converting an experiment kind into an executable contract.
/// AI may choose the kind, but it cannot weaken success/falsification conditions
/// or erase required evidence. Callers may provide runner-specific configuration
/// through ConfigurationJson or .dip/autonomous-qa.json.
/// </summary>
public static class AutonomousQaExperimentDefaults
{
    public static AutonomousQaExperimentStep Create(
        string id,
        DebugExperimentKind kind,
        string reason,
        bool required,
        int timeoutSeconds,
        string? configurationJson = null)
    {
        var timeout = Math.Clamp(timeoutSeconds, 30, 1800);
        var (success, falsification, evidence) = kind switch
        {
            DebugExperimentKind.Compile => (
                "All available build/type-check tools complete successfully.",
                "A build or type-check failure reproduces the defect boundary.",
                Evidence("tool-execution", "Build/type-check execution diagnostics.")),
            DebugExperimentKind.StaticAnalysis => (
                "No required static-analysis tool fails and no new Critical finding is produced.",
                "A deterministic static finding supports or narrows the hypothesis.",
                Evidence("finding-set", "Static-analysis findings and tool diagnostics.")),
            DebugExperimentKind.UnitTest or DebugExperimentKind.RegressionTest => (
                "Required automated tests complete without a blocking failure.",
                "A deterministic failing test reproduces the behavior under test.",
                Evidence("test-result", "Automated test execution evidence.")),
            DebugExperimentKind.AssetValidation => (
                "Asset/production QA completes without a blocking finding.",
                "A deterministic asset-integrity finding supports the hypothesis.",
                Evidence("asset-qa", "Asset validation findings.")),
            DebugExperimentKind.ShaderValidation => (
                "Shader/compiler QA completes without a blocking finding.",
                "Shader compile/validation failure supports the hypothesis.",
                Evidence("shader-qa", "Shader compile or validation diagnostics.")),
            DebugExperimentKind.VisualRegression => (
                "Visual difference remains below the configured failure threshold.",
                "A visual difference at or above the failure threshold reproduces the regression.",
                Evidence("visual-diff", "Visual regression artifact and comparison metrics.")),
            DebugExperimentKind.VideoRegression => (
                "Video difference remains below the configured failure threshold.",
                "A video difference at or above the failure threshold reproduces the regression.",
                Evidence("video-diff", "Video regression artifact and comparison metrics.")),
            DebugExperimentKind.AudioRegression => (
                "Audio difference remains below the configured failure threshold.",
                "An audio difference at or above the failure threshold reproduces the regression.",
                Evidence("audio-diff", "Audio regression artifact and comparison metrics.")),
            DebugExperimentKind.Reproduce => (
                "The configured reproduction path completes without reproducing the blocking defect.",
                "The same defect fingerprint is reproduced under the configured path.",
                Evidence("reproduction", "Runtime/test reproduction evidence.")),
            DebugExperimentKind.RuntimeReplay => (
                "Deterministic replay completes without crash, timeout, or configured regression.",
                "Replay reproduces a crash, timeout, or deterministic runtime regression.",
                Evidence("runtime-replay", "Replay exit state, logs, and runtime metrics.")),
            DebugExperimentKind.Benchmark or DebugExperimentKind.LoadTest => (
                "All measured benchmark/load iterations satisfy the configured execution boundary.",
                "A repeatable benchmark/load failure or timeout reproduces the performance/service defect.",
                Evidence("benchmark", "Benchmark samples and environment fingerprint.")),
            DebugExperimentKind.Stress or DebugExperimentKind.Soak => (
                "Sustained execution completes without crash and without configured memory/performance drift.",
                "Crash, timeout, memory growth, or sustained performance drift reproduces the defect.",
                Evidence("soak", "Sustained-execution telemetry and artifact bundle.")),
            DebugExperimentKind.Fuzz => (
                "Fuzz execution completes without crash or timeout findings.",
                "A minimized crash/timeout input reproduces the defect.",
                Evidence("fuzz", "Fuzz corpus/result and failure fingerprints.")),
            DebugExperimentKind.Mutation => (
                "Mutation execution reaches the configured quality threshold.",
                "Surviving mutations identify a test-coverage weakness relevant to the hypothesis.",
                Evidence("mutation", "Mutation score and surviving mutation evidence.")),
            DebugExperimentKind.Bisect => (
                "A bounded revision search returns a deterministic regression boundary.",
                "No stable revision boundary can be established from the supplied range.",
                Evidence("bisect", "Evaluated revisions and first-bad/last-good evidence.")),
            DebugExperimentKind.MemorySafety => (
                "Memory-safety QA completes without a blocking sanitizer/runtime finding.",
                "A deterministic memory-safety finding reproduces the defect class.",
                Evidence("memory-safety", "Sanitizer/runtime diagnostics.")),
            DebugExperimentKind.RaceDetection or DebugExperimentKind.DeadlockDetection => (
                "Concurrency QA completes without a blocking race/deadlock finding.",
                "A deterministic race/deadlock finding reproduces the defect class.",
                Evidence("concurrency", "Concurrency diagnostics and reproduction evidence.")),
            DebugExperimentKind.Security => (
                "Security QA completes without a blocking security finding.",
                "A deterministic security finding supports the hypothesis.",
                Evidence("security", "Security findings and tool diagnostics.")),
            DebugExperimentKind.VerifyPatch => (
                "The patch applies cleanly and all required post-patch experiments pass.",
                "Patch application or any required verification step fails.",
                Evidence("patch-verification", "Patch application and verification summary.")),
            _ => (
                "The configured experiment completes with verifiable evidence.",
                "The experiment produces repeatable evidence contradicting the expected behavior.",
                Evidence("experiment", "Experiment result evidence."))
        };

        return new(
            id,
            kind,
            required,
            reason,
            success,
            falsification,
            evidence,
            configurationJson,
            timeout);
    }

    private static IReadOnlyList<AutonomousQaRequiredEvidence> Evidence(
        string kind,
        string description) =>
        [new(kind, description, true)];
}
