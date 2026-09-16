// Purpose: Defines the HardwareProfileDto application contract.
namespace Dip.Application.Features.Hardware;

public sealed record HardwareProfileDto(Guid Id,string Name,string Platform,string Source,decimal CpuSingleScore,decimal CpuMultiScore,decimal GpuScore,decimal MemoryScore,decimal StorageScore,decimal ThermalScore,long MemoryBudgetMb,int TargetFps,string MetadataJson);
