// Purpose: Defines CommunityHardwareSubmission.
namespace Dip.Application.Features.Intelligence;

public sealed record CommunityHardwareSubmission(string HardwareKey, string Platform, string CpuArch, string GpuVendor, string OsFamily, string DriverFamily, decimal CpuSingleScore, decimal CpuMultiScore, decimal GpuScore, decimal MemoryScore, decimal StorageScore, decimal ThermalScore, long MemoryMb, string ConsentToken, string? ClientPseudonym = null);
