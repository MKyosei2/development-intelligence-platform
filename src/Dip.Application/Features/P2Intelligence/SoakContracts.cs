// Purpose: Defines long-running soak-test commands, telemetry samples, and result contracts.
namespace Dip.Application.Features.P2Intelligence;

public sealed record SoakCommand(
    ExperimentTarget Target,
    int DurationSeconds = 1800,
    int SampleIntervalSeconds = 5,
    decimal MemoryGrowthWarningPercent = 20m,
    decimal PerformanceDriftWarningPercent = 15m);

public sealed record SoakTelemetryPoint(
    int ElapsedSeconds,
    long WorkingSetMb,
    decimal CpuPercent,
    decimal? Fps,
    decimal? FrameTimeMs);

public sealed record SoakResult(
    string Status,
    long StartMemoryMb,
    long EndMemoryMb,
    decimal MemoryGrowthPercent,
    decimal? StartFps,
    decimal? EndFps,
    decimal? PerformanceDriftPercent,
    bool Crashed,
    bool TimedOut,
    IReadOnlyList<SoakTelemetryPoint> Samples,
    string ArtifactReference);
