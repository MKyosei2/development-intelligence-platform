namespace Dip.Application.Features.Hardware;

public sealed record HardwareCalibrationMeasurement(
    decimal CpuSingleScore,
    decimal CpuMultiScore,
    decimal GpuScore,
    decimal MemoryScore,
    decimal StorageScore,
    long AvailableMemoryMb,
    string MachineFingerprint,
    string EnvironmentJson,
    string RawResultJson);

public sealed record HardwareProfileDto(
    Guid Id,string Name,string Platform,string Source,decimal CpuSingleScore,decimal CpuMultiScore,decimal GpuScore,
    decimal MemoryScore,decimal StorageScore,decimal ThermalScore,long MemoryBudgetMb,int TargetFps,string MetadataJson);

public sealed record CreateRelativeHardwareProfileCommand(
    Guid ProjectId,string Name,string Platform,decimal CpuPercent,decimal GpuPercent,decimal MemoryBandwidthPercent,
    decimal StoragePercent,decimal ThermalRetentionPercent,long MemoryBudgetMb,int TargetFps);

public sealed record PerformanceSimulationCommand(
    Guid ProjectId,Guid HardwareProfileId,string SubjectKey,string WorkspaceRelativePath,string Executable,
    IReadOnlyList<string>? Arguments,int DurationSeconds=60,bool ThermalSimulation=true);

public sealed record PerformanceSimulationMeasurement(
    string Status,decimal ConfidencePercent,bool TimedOut,int? ExitCode,long DurationMs,long PeakWorkingSetMb,
    decimal? AverageFps,decimal? P95FrameTimeMs,string ArtifactDirectory,string ResultJson);

public sealed record PerformanceSimulationDto(Guid Id,string Status,decimal ConfidencePercent,string ResultJson,string ArtifactReference);

public sealed record MinimumHardwareEstimateCommand(
    Guid ProjectId,string SubjectKey,string WorkspaceRelativePath,string Executable,IReadOnlyList<string>? Arguments,
    int DurationSeconds=30,int TargetFps=30,long MemoryBudgetMb=4096);

public sealed record MinimumHardwareEstimateDto(
    decimal EstimatedCpuPercent,decimal EstimatedGpuPercent,decimal ConfidencePercent,string Status,
    IReadOnlyList<string> TestedPoints,string EvidenceNote)
{
    public decimal EstimatedMemoryBandwidthPercent { get; init; }
    public decimal EstimatedStoragePercent { get; init; }
    public long EstimatedMemoryBudgetMb { get; init; }
    public decimal RecommendedCpuPercent { get; init; }
    public decimal RecommendedGpuPercent { get; init; }
    public decimal RecommendedMemoryBandwidthPercent { get; init; }
    public decimal RecommendedStoragePercent { get; init; }
    public long RecommendedMemoryBudgetMb { get; init; }
}

public interface IHardwareCalibrationRunner
{
    Task<HardwareCalibrationMeasurement> CalibrateAsync(string artifactDirectory,CancellationToken cancellationToken);
}
public interface IPerformanceSimulationRunner
{
    Task<PerformanceSimulationMeasurement> RunAsync(
        HardwareProfileDto profile,string workspace,string executable,IReadOnlyList<string> arguments,int durationSeconds,bool thermalSimulation,string artifactDirectory,CancellationToken cancellationToken);
}
