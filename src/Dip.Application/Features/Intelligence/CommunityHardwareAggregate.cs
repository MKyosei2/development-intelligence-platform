// Purpose: Defines CommunityHardwareAggregate.
namespace Dip.Application.Features.Intelligence;

public sealed record CommunityHardwareAggregate(string HardwareKey,int SampleCount,decimal CpuMedian,decimal CpuP10,decimal CpuP90,decimal GpuMedian,decimal GpuP10,decimal GpuP90,decimal MemoryMedian,decimal StorageMedian,decimal ThermalMedian,decimal ConfidencePercent);
