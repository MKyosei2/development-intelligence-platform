// Purpose: Defines visual, video, and audio regression-analysis commands, results, and runner boundary.
namespace Dip.Application.Features.P2Intelligence;

public enum MediaRegressionType
{
    Visual,
    Video,
    Audio
}

public sealed record MediaRegressionCommand(
    Guid ProjectId,
    string SubjectKey,
    string WorkspaceRelativePath,
    string BaselinePath,
    string CurrentPath,
    MediaRegressionType Type,
    decimal WarningThresholdPercent = 2m,
    decimal FailureThresholdPercent = 10m,
    int VideoSampleFps = 2);

public sealed record MediaRegressionResult(
    string Status,
    MediaRegressionType Type,
    decimal DifferencePercent,
    decimal ConfidencePercent,
    string? FirstDifference,
    string Summary,
    string ArtifactReference,
    string DetailJson);

public interface IMediaRegressionRunner
{
    Task<MediaRegressionResult> AnalyzeAsync(
        MediaRegressionCommand command,
        CancellationToken cancellationToken);
}
