using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

public sealed record QaExecutionCommand(
    Guid ProjectId,
    string SubjectKey,
    QaLanguage Language,
    string WorkspaceRelativePath,
    string? CommitSha,
    string? RuntimeExecutable,
    IReadOnlyList<string>? RuntimeArguments,
    int TimeoutSeconds = 120,
    bool AutomaticallyReproduce = true,
    bool RecordScreen = true,
    bool RecordSystemAudio = true);

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
    QaLanguage Language,
    IReadOnlyList<QaToolExecution> Tools,
    IReadOnlyList<QaFindingCandidate> Findings,
    string SummaryJson);

public sealed record ReproductionScenario(
    Guid ProjectId,
    Guid FindingId,
    QaLanguage Language,
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

public interface IPythonHardcodedQaRunner
{
    Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken cancellationToken);
}
public interface ICSharpHardcodedQaRunner
{
    Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken cancellationToken);
}
public interface ICppHardcodedQaRunner
{
    Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken cancellationToken);
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
