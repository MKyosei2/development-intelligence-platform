using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public sealed record QaExecutionCommand(
    Guid ProjectId,
    string SubjectKey,
    LanguageId? Language,
    string WorkspaceRelativePath,
    string? CommitSha,
    string? RuntimeExecutable,
    IReadOnlyList<string>? RuntimeArguments,
    int TimeoutSeconds = 120,
    bool AutomaticallyReproduce = true,
    bool RecordScreen = true,
    bool RecordSystemAudio = true,
    bool AutoDetectTechnologies = true);

public sealed record QaFindingCandidate(
    string Category,
    Severity Severity,
    string Tool,
    string Title,
    string Message,
    string? FilePath = null,
    int? Line = null,
    int? Column = null,
    string? Function = null,
    string? ExceptionType = null,
    string? StackTrace = null,
    string? RuleId = null);

public sealed record QaToolExecution(
    string Tool,
    bool WasAvailable,
    bool Succeeded,
    int? ExitCode,
    long DurationMs,
    string StandardOutputPath,
    string StandardErrorPath,
    string Summary);

public sealed record HardcodedQaResult(
    LanguageId Language,
    IReadOnlyList<QaToolExecution> Tools,
    IReadOnlyList<QaFindingCandidate> Findings,
    string SummaryJson);

public sealed record WorkspaceTechnologyProfile(
    IReadOnlyList<LanguageId> Languages,
    IReadOnlyList<string> BuildSystems,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<string> GameEngines,
    IReadOnlyList<string> Evidence)
{
    public static WorkspaceTechnologyProfile Empty { get; } = new([], [], [], [], []);
    public LanguageId? PrimaryLanguage => Languages.Count == 0 ? null : Languages[0];
}

public sealed record DiagnosticDocument(
    string Tool,
    string Format,
    string Content,
    string? SourcePath = null,
    Severity FallbackSeverity = Severity.Warning,
    string FallbackCategory = "diagnostic");

public sealed record ReproductionScenario(
    Guid ProjectId,
    Guid FindingId,
    LanguageId Language,
    string WorkspacePath,
    string QaRunArtifactRoot,
    string Executable,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string,string> EnvironmentVariables,
    string ExpectedFingerprint,
    int TimeoutSeconds,
    int AttemptCount,
    bool RecordScreen,
    bool RecordSystemAudio);

public sealed record ReproductionAttemptResult(
    int AttemptNumber,
    ReproductionStatus Status,
    bool Reproduced,
    bool Crashed,
    bool TimedOut,
    int? ExitCode,
    long DurationMs,
    string ArtifactDirectory,
    string? VideoPath,
    string? AudioPath,
    string? DumpPath,
    string ResultJson);

public sealed record ReproductionResult(
    ReproductionStatus Status,
    int ReproducedAttempts,
    int TotalAttempts,
    IReadOnlyList<ReproductionAttemptResult> Attempts);

public sealed record QaRunResultDto(
    Guid QaRunId,
    string Status,
    string Language,
    int FindingCount,
    int CriticalFindingCount,
    int WarningFindingCount,
    IReadOnlyList<QaFindingDto> Findings,
    string ArtifactReference);

public sealed record QaArtifactDto(string RelativePath,long Length,string Kind);

public sealed record QaFindingDto(
    Guid Id,
    string Category,
    string Severity,
    string Tool,
    string Title,
    string Message,
    string Fingerprint,
    string? FilePath,
    int? Line,
    string ReproductionStatus,
    int ReproducedAttempts,
    int TotalAttempts);

public sealed record QaCrashClusterDto(
    string Fingerprint,
    string Category,
    string Severity,
    string Title,
    int Occurrences,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    int ReproducedOccurrences,
    IReadOnlyList<string> Subjects);

public interface IQaAdapter
{
    LanguageId Language { get; }
    int Priority => 0;
    bool CanHandle(WorkspaceTechnologyProfile profile) => profile.Languages.Contains(Language);
    Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken cancellationToken);
}

public interface IQaAdapterRegistry
{
    IReadOnlyList<LanguageId> SupportedLanguages { get; }
    IQaAdapter Resolve(LanguageId language);
    bool TryResolve(LanguageId language,out IQaAdapter? adapter);
}

public interface IWorkspaceTechnologyDetector
{
    Task<WorkspaceTechnologyProfile> DetectAsync(string workspace,CancellationToken cancellationToken);
}

public interface IDiagnosticNormalizer
{
    IReadOnlyList<QaFindingCandidate> Normalize(LanguageId language,DiagnosticDocument document);
}

public interface IReproductionCoordinator
{
    Task<ReproductionResult> ReproduceAsync(ReproductionScenario scenario,CancellationToken cancellationToken);
}

public interface IQaWorkspaceResolver
{
    string Resolve(string workspaceRelativePath);
    string QaArtifactRoot(Guid projectId,Guid qaRunId);
}
