// Purpose: Defines the CreateRelativeHardwareProfileCommand application contract.
namespace Dip.Application.Features.Hardware;

public sealed record CreateRelativeHardwareProfileCommand(Guid ProjectId,string Name,string Platform,decimal CpuPercent,decimal GpuPercent,decimal MemoryBandwidthPercent,decimal StoragePercent,decimal ThermalRetentionPercent,long MemoryBudgetMb,int TargetFps);
