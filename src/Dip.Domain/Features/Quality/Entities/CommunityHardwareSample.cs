// Purpose: Defines domain behavior and state for this feature area.
namespace Dip.Domain.Entities;
public sealed class CommunityHardwareSample
{
    private CommunityHardwareSample() { }
    public CommunityHardwareSample(Guid id, string hardwareKey, string platform, string cpuArch, string gpuVendor, string osFamily, string driverFamily,
        decimal cpuSingle, decimal cpuMulti, decimal gpu, decimal memory, decimal storage, decimal thermal, long memoryMb, string sourceFingerprint, DateTimeOffset measuredAtUtc)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id; HardwareKey = Req(hardwareKey, 160); Platform = Req(platform, 80); CpuArch = Req(cpuArch, 80);
        GpuVendor = Req(gpuVendor, 80); OsFamily = Req(osFamily, 80); DriverFamily = Opt(driverFamily, 160);
        CpuSingleScore = NN(cpuSingle); CpuMultiScore = NN(cpuMulti); GpuScore = NN(gpu); MemoryScore = NN(memory); StorageScore = NN(storage); ThermalScore = NN(thermal);
        MemoryMb = memoryMb < 128 ? throw new ArgumentOutOfRangeException(nameof(memoryMb)) : memoryMb;
         SourceFingerprint = Req(sourceFingerprint, 64);
         MeasuredAtUtc = measuredAtUtc;
    }
    public Guid Id { get; private set; }
    public string HardwareKey { get; private set; } = string.Empty; public string Platform { get; private set; } = string.Empty;
    public string CpuArch { get;
     private set;
     } = string.Empty;
     public string GpuVendor { get;
     private set;
     } = string.Empty;
     public string OsFamily { get;
     private set;
     } = string.Empty;
    public string? DriverFamily
    {
        get;
        private set;
    }
    public decimal CpuSingleScore
    {
        get;
        private set;
    }
    public decimal CpuMultiScore
    {
        get;
        private set;
    }
    public decimal GpuScore
    {
        get;
        private set;
    }
    public decimal MemoryScore { get; private set; }
    public decimal StorageScore { get; private set; }
    public decimal ThermalScore { get; private set; }
    public long MemoryMb { get; private set; }
    public string SourceFingerprint { get; private set; } = string.Empty; public DateTimeOffset MeasuredAtUtc { get; private set; }
    static decimal NN(decimal x) => x < 0 ? throw new ArgumentOutOfRangeException() : decimal.Round(x, 3);
    static string Req(string? x, int m)
    {
        var s = x?.Trim() ?? "";
        if (s.Length < 1 || s.Length > m) throw new ArgumentException();
        return s;
    }
    static string? Opt(string? x, int m)
    {
        if (string.IsNullOrWhiteSpace(x)) return null;
        var s = x.Trim();
        if (s.Length > m) throw new ArgumentException();
        return s;
    }
}
