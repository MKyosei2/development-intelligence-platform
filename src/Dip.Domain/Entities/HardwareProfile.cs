using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class HardwareProfile
{
    private HardwareProfile() { }

    public HardwareProfile(
        Guid id,
        string name,
        string platform,
        HardwareProfileSource source,
        decimal cpuSingleScore,
        decimal cpuMultiScore,
        decimal gpuScore,
        decimal memoryScore,
        decimal storageScore,
        decimal thermalScore,
        long memoryBudgetMb,
        int targetFps,
        string metadataJson,
        DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = Required(name, 160, nameof(name));
        Platform = Required(platform, 80, nameof(platform));
        Source = source;
        CpuSingleScore = NonNegative(cpuSingleScore, nameof(cpuSingleScore));
        CpuMultiScore = NonNegative(cpuMultiScore, nameof(cpuMultiScore));
        GpuScore = NonNegative(gpuScore, nameof(gpuScore));
        MemoryScore = NonNegative(memoryScore, nameof(memoryScore));
        StorageScore = NonNegative(storageScore, nameof(storageScore));
        ThermalScore = NonNegative(thermalScore, nameof(thermalScore));
        MemoryBudgetMb = memoryBudgetMb < 128 ? throw new ArgumentOutOfRangeException(nameof(memoryBudgetMb)) : memoryBudgetMb;
        TargetFps = targetFps is < 1 or > 1000 ? throw new ArgumentOutOfRangeException(nameof(targetFps)) : targetFps;
        MetadataJson = Required(metadataJson, 32000, nameof(metadataJson));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public HardwareProfileSource Source { get; private set; }
    public decimal CpuSingleScore { get; private set; }
    public decimal CpuMultiScore { get; private set; }
    public decimal GpuScore { get; private set; }
    public decimal MemoryScore { get; private set; }
    public decimal StorageScore { get; private set; }
    public decimal ThermalScore { get; private set; }
    public long MemoryBudgetMb { get; private set; }
    public int TargetFps { get; private set; }
    public string MetadataJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static decimal NonNegative(decimal value, string name) => value < 0 ? throw new ArgumentOutOfRangeException(name) : decimal.Round(value, 3);
    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
