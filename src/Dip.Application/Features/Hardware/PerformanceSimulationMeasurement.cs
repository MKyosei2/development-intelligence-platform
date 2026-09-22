// Purpose: Defines the PerformanceSimulationMeasurement application contract.
namespace Dip.Application.Features.Hardware;

public sealed record PerformanceSimulationMeasurement(string Status, decimal ConfidencePercent, bool TimedOut, int? ExitCode, long DurationMs, long PeakWorkingSetMb, decimal? AverageFps, decimal? P95FrameTimeMs, string ArtifactDirectory, string ResultJson);
