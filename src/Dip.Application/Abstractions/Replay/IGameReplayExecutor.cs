// Purpose: Defines the I Ga me Re pl ay Ex ec ut or application boundary.
namespace Dip.Application.Abstractions.Replay;

public sealed record ReplayExecutionRequest(string Command,string Arguments,string? WorkingDirectory,string Scene,string InputArtifactId,int RandomSeed,int DurationSeconds,string CommitSha,string BuildId,string HardwareFingerprint);
public sealed record ReplaySample(string Name,decimal Value,string Unit);
public sealed record ReplayExecutionResult(bool Succeeded,int ExitCode,bool Crashed,bool TimedOut,string StandardOutput,string StandardError,IReadOnlyList<ReplaySample> Samples,string EnvironmentFingerprint);
public interface IGameReplayExecutor
{
    Task<ReplayExecutionResult> ExecuteAsync(ReplayExecutionRequest request,CancellationToken ct);
}
