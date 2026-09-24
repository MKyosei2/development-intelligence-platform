// Purpose: Implements Qa Capabilities responsibilities for quality automation.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public enum QaCapability
{
    Compile, TypeCheck, StaticAnalysis, AstAnalysis, DataFlowAnalysis, UnitTest,
    PropertyTest, Fuzz, RaceDetection, DeadlockDetection, Runtime, Security,
    ApiContract, Database, DistributedSystem, GameEngine, LspDiagnostics
}

public sealed record QaCapabilityDescriptor(QaCapability Capability, string Tool, string Detail, bool RequiresProjectConfiguration = false);
public sealed record QaLanguagePack(LanguageId Language, IReadOnlyList<QaCapabilityDescriptor> Capabilities);
public sealed record QaSemanticAnalysisRequest(LanguageId Language, string Workspace, int MaximumFiles = 1000);
public sealed record QaSemanticAnalysisResult(IReadOnlyList<QaFindingCandidate> Findings, IReadOnlyList<string> Evidence, int FilesAnalyzed);

public interface IQaCapabilityPlanner
{
    QaLanguagePack Describe(LanguageId language, QaTechnologyProfile profile, string workspace);
}

public interface IUniversalSemanticAnalyzer
{
    Task<QaSemanticAnalysisResult> AnalyzeAsync(QaSemanticAnalysisRequest request, CancellationToken ct);
}

public sealed record RuntimeDefectObservation(
    Guid ProjectId, string SubjectKey, LanguageId Language, string Source, string Title, string Message,
    string? ExceptionType, string? StackTrace, string? CommitSha, string? BuildId, string? SceneOrRoute,
    string? FilePath, int? Line, IReadOnlyDictionary<string, string>? Tags, DateTimeOffset ObservedAtUtc);
public sealed record RuntimeDefectResult(Guid IncidentId, string Fingerprint, string Severity, string Category, string Summary, IReadOnlyList<string> Signals);
public interface IRuntimeDefectIntelligence
{
    Task<RuntimeDefectResult> ObserveAsync(RuntimeDefectObservation observation, CancellationToken ct);
}

public sealed record RegressionTestDraft(string RelativePath, string Content, string Purpose);
public sealed record VerifiedFixRequest(
    Guid ProjectId, string WorkspaceRelativePath, string? BaseCommit, string UnifiedDiff,
    IReadOnlyList<RegressionTestDraft> RegressionTests, LanguageId Language, int TimeoutSeconds = 900);
public sealed record VerifiedFixResult(
    bool Applied, bool Verified, string Status, string? CommitUnderTest, string ArtifactRoot,
    IReadOnlyList<string> ValidationSteps, IReadOnlyList<QaFindingCandidate> Findings, string Summary);
public interface IVerifiedFixService
{
    Task<VerifiedFixResult> VerifyAsync(VerifiedFixRequest request, CancellationToken ct);
}

public sealed record IdeDiagnosticBatch(Guid ProjectId, string SubjectKey, LanguageId Language, string WorkspaceRelativePath, string? CommitSha, IReadOnlyList<IdeDiagnostic> Diagnostics);
public sealed record IdeDiagnostic(string Source, string Code, string Severity, string Message, string? FilePath, int? Line, int? Column);
public interface IIdeDiagnosticNormalizer
{
    IReadOnlyList<QaFindingCandidate> Normalize(IdeDiagnosticBatch batch);
}

public sealed record FixProposalRequest(Guid ProjectId, string SubjectKey, string Question, string EvidenceJson, string? RepositoryContext);
public sealed record FixProposalResponse(string Provider, string Model, string Summary, string UnifiedDiff, IReadOnlyList<RegressionTestDraft> RegressionTests, IReadOnlyList<Guid> EvidenceIds);
public interface IFixProposalAgent
{
    Task<FixProposalResponse?> ProposeAsync(FixProposalRequest request, CancellationToken ct);
}

// Autonomous debugging deliberately separates AI reasoning from verification.
// AI may propose hypotheses/patches, but only isolated executable evidence may
// move a session to ReadyForHumanReview.
public enum DebugExperimentKind
{
    Reproduce, VerifyPatch, RegressionTest, StaticAnalysis, RuntimeReplay, Benchmark, Bisect, Stress, Fuzz
}

public sealed record DebugHypothesis(
    string Id, string Title, string Explanation, decimal Confidence,
    IReadOnlyList<Guid> EvidenceIds, IReadOnlyList<DebugExperimentKind> SuggestedExperiments);
public sealed record DebugHypothesisRequest(
    Guid ProjectId, string SubjectKey, string Question, string EvidenceJson, string? RepositoryContext, int MaxHypotheses = 5);
public sealed record DebugHypothesisResponse(string Provider, string Model, IReadOnlyList<DebugHypothesis> Hypotheses);
public interface IAutonomousDebugAgent
{
    Task<DebugHypothesisResponse?> GenerateHypothesesAsync(DebugHypothesisRequest request, CancellationToken ct);
}

public sealed record AutonomousDebugBudget(
    int MaxHypotheses = 4, int MaxExperiments = 3, int MaxAiCalls = 5, int TimeoutSecondsPerExperiment = 900, int MaxWallClockMinutes = 30)
{
    public AutonomousDebugBudget Validate()
    {
        if (MaxHypotheses is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(MaxHypotheses), "MaxHypotheses must be 1-10.");
        if (MaxExperiments is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(MaxExperiments), "MaxExperiments must be 1-10.");
        if (MaxAiCalls is < 2 or > 20) throw new ArgumentOutOfRangeException(nameof(MaxAiCalls), "MaxAiCalls must be 2-20.");
        if (TimeoutSecondsPerExperiment is < 30 or > 1800) throw new ArgumentOutOfRangeException(nameof(TimeoutSecondsPerExperiment), "TimeoutSecondsPerExperiment must be 30-1800.");
        if (MaxWallClockMinutes is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(MaxWallClockMinutes), "MaxWallClockMinutes must be 1-120.");
        return this;
    }
}

public sealed record AutonomousDebugRequest(
    Guid ProjectId, string SubjectKey, string Question, string EvidenceJson, string WorkspaceRelativePath,
    string? RepositoryContext, string? BaseCommit, LanguageId Language, string DomainId = "software", AutonomousDebugBudget? Budget = null);
public sealed record AutonomousDebugAttemptDto(
    string HypothesisId, string HypothesisTitle, decimal Confidence, string Status, bool Verified, string Summary,
    string? ArtifactRoot, IReadOnlyList<Guid> EvidenceIds);
public sealed record AutonomousDebugSessionDto(
    Guid SessionId, Guid ProjectId, string SubjectKey, string DomainId, string Status, string Summary,
    IReadOnlyList<DebugHypothesis> Hypotheses, IReadOnlyList<AutonomousDebugAttemptDto> Attempts,
    DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc);

public sealed record QaCrossCuttingResult(string PackId, IReadOnlyList<QaToolExecution> Tools, IReadOnlyList<QaFindingCandidate> Findings, string SummaryJson);
public interface ICrossCuttingQaPack
{
    string PackId { get; }
    bool Applies(QaTechnologyProfile profile, string workspace);
    Task<QaCrossCuttingResult> RunAsync(QaExecutionCommand command, QaTechnologyProfile profile, string workspace, string artifactRoot, CancellationToken ct);
}
