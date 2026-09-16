// Purpose: Defines the MinimumHardwareResult application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record MinimumHardwareResult(decimal CpuPercent,decimal GpuPercent,decimal MemoryPercent,decimal StoragePercent,decimal SafeCpuPercent,decimal SafeGpuPercent,decimal SafeMemoryPercent,decimal SafeStoragePercent,decimal ConfidencePercent,string EvidenceJson);
