// Purpose: Defines the HardwareCalibrationMeasurement application contract.
namespace Dip.Application.Features.Hardware;

public sealed record HardwareCalibrationMeasurement(decimal CpuSingleScore, decimal CpuMultiScore, decimal GpuScore, decimal MemoryScore, decimal StorageScore, long AvailableMemoryMb, string MachineFingerprint, string EnvironmentJson, string RawResultJson);
