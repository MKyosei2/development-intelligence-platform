// Purpose: Represents H ar dw ar eC al ib ra ti on in the Quality domain.
namespace Dip.Domain.Entities;

public sealed class HardwareCalibration
{
    private HardwareCalibration() { }

    public HardwareCalibration(
        Guid id,
        Guid projectId,
        string machineFingerprint,
        decimal cpuSingleScore,
        decimal cpuMultiScore,
        decimal gpuScore,
        decimal memoryScore,
        decimal storageScore,
        long availableMemoryMb,
        string environmentJson,
        DateTimeOffset measuredAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        MachineFingerprint = Required(machineFingerprint, 64, nameof(machineFingerprint));
        CpuSingleScore = Math.Max(0m, cpuSingleScore);
        CpuMultiScore = Math.Max(0m, cpuMultiScore);
        GpuScore = Math.Max(0m, gpuScore);
        MemoryScore = Math.Max(0m, memoryScore);
        StorageScore = Math.Max(0m, storageScore);
        AvailableMemoryMb = Math.Max(0, availableMemoryMb);
        EnvironmentJson = Required(environmentJson, 32000, nameof(environmentJson));
        MeasuredAtUtc = measuredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string MachineFingerprint { get; private set; } = string.Empty;
    public decimal CpuSingleScore { get; private set; }
    public decimal CpuMultiScore { get; private set; }
    public decimal GpuScore { get; private set; }
    public decimal MemoryScore { get; private set; }
    public decimal StorageScore { get; private set; }
    public long AvailableMemoryMb { get; private set; }
    public string EnvironmentJson { get; private set; } = string.Empty;
    public DateTimeOffset MeasuredAtUtc { get; private set; }

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
