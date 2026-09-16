// Purpose: Represents H ar dw ar eC om mu ni ty Sa mp le in the Quality domain.
namespace Dip.Domain.Entities;

/// <summary>
/// Opt-in, privacy-minimised calibration sample. No account name, raw machine
/// identifier, file path, IP address or project content is stored here.
/// </summary>
public sealed class HardwareCommunitySample
{
    private HardwareCommunitySample() { }

    public HardwareCommunitySample(
        Guid id,
        string profileKey,
        string platform,
        string hardwareKeyHash,
        string osFamily,
        string driverFamily,
        decimal cpuSingleScore,
        decimal cpuMultiScore,
        decimal gpuScore,
        decimal memoryScore,
        decimal storageScore,
        decimal thermalRetentionPercent,
        long availableMemoryMb,
        bool realDevice,
        bool consentGranted,
        DateTimeOffset measuredAtUtc,
        DateTimeOffset receivedAtUtc)
    {
        if (!consentGranted) throw new InvalidOperationException("Community hardware submission requires explicit opt-in consent.");
        Id=id==Guid.Empty?Guid.NewGuid():id;
        ProfileKey=Required(profileKey,160,nameof(profileKey));
        Platform=Required(platform,80,nameof(platform));
        HardwareKeyHash=Required(hardwareKeyHash,64,nameof(hardwareKeyHash));
        OsFamily=Required(osFamily,120,nameof(osFamily));
        DriverFamily=Required(driverFamily,160,nameof(driverFamily));
        CpuSingleScore=NonNegative(cpuSingleScore,nameof(cpuSingleScore));
        CpuMultiScore=NonNegative(cpuMultiScore,nameof(cpuMultiScore));
        GpuScore=NonNegative(gpuScore,nameof(gpuScore));
        MemoryScore=NonNegative(memoryScore,nameof(memoryScore));
        StorageScore=NonNegative(storageScore,nameof(storageScore));
        ThermalRetentionPercent=Math.Clamp(thermalRetentionPercent,0m,100m);
        AvailableMemoryMb=availableMemoryMb<128?throw new ArgumentOutOfRangeException(nameof(availableMemoryMb)):availableMemoryMb;
        RealDevice=realDevice;
        ConsentGranted=true;
        MeasuredAtUtc=measuredAtUtc;
        ReceivedAtUtc=receivedAtUtc;
    }

    public Guid Id{get;private set;}
    public string ProfileKey{get;private set;}=string.Empty;
    public string Platform{get;private set;}=string.Empty;
    public string HardwareKeyHash{get;private set;}=string.Empty;
    public string OsFamily{get;private set;}=string.Empty;
    public string DriverFamily{get;private set;}=string.Empty;
    public decimal CpuSingleScore{get;private set;}
    public decimal CpuMultiScore{get;private set;}
    public decimal GpuScore{get;private set;}
    public decimal MemoryScore{get;private set;}
    public decimal StorageScore{get;private set;}
    public decimal ThermalRetentionPercent{get;private set;}
    public long AvailableMemoryMb{get;private set;}
    public bool RealDevice{get;private set;}
    public bool ConsentGranted{get;private set;}
    public DateTimeOffset MeasuredAtUtc{get;private set;}
    public DateTimeOffset ReceivedAtUtc{get;private set;}

    private static decimal NonNegative(decimal value,string name)=>value<0?throw new ArgumentOutOfRangeException(name):decimal.Round(value,3);
    private static string Required(string? value,int max,string name)
    {
        var text=value?.Trim()??string.Empty;
        if(text.Length is < 1||text.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);
        return text;
    }
}
