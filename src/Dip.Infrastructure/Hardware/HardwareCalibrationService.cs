// Purpose: Provides the H ar dw ar eC al ib ra ti on Se rv ic e infrastructure implementation.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.Hardware;
using Dip.Infrastructure.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Hardware;

public sealed class HardwareCalibrationService(ProcessExecutor executor,IOptions<QaExecutionOptions> qaOptions):IHardwareCalibrationRunner
{
    public async Task<HardwareCalibrationMeasurement> CalibrateAsync(string artifactDirectory,CancellationToken ct)
    {
        Directory.CreateDirectory(artifactDirectory);
        var cpuSingle=MeasureSingleCore();var cpuMulti=MeasureMultiCore();var memory=MeasureMemoryBandwidth();var storage=await MeasureStorageAsync(artifactDirectory,ct);
        var gpu=await MeasureGpuAsync(artifactDirectory,ct);var available=GetAvailableMemoryMb();
        var environment=new{os=RuntimeInformation.OSDescription,osArch=RuntimeInformation.OSArchitecture.ToString(),processArch=RuntimeInformation.ProcessArchitecture.ToString(),cpuCount=Environment.ProcessorCount,gpu.adapter,gpu.available,availableMemoryMb=available};
        var envJson=JsonSerializer.Serialize(environment,new JsonSerializerOptions{WriteIndented=true});
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Environment.MachineName}|{RuntimeInformation.OSDescription}|{Environment.ProcessorCount}|{gpu.adapter}")));
        var raw=JsonSerializer.Serialize(new{cpuSingleScore=cpuSingle,cpuMultiScore=cpuMulti,gpuScore=gpu.score,memoryScore=memory,storageScore=storage,availableMemoryMb=available,machineFingerprint=fingerprint,environment},new JsonSerializerOptions{WriteIndented=true});
        await File.WriteAllTextAsync(Path.Combine(artifactDirectory,"calibration.json"),raw,ct);await File.WriteAllTextAsync(Path.Combine(artifactDirectory,"environment.json"),envJson,ct);
        return new(cpuSingle,cpuMulti,gpu.score,memory,storage,available,fingerprint,envJson,raw);
    }

    private static decimal MeasureSingleCore()
    {
        const int iterations=12_000_000;ulong value=1469598103934665603UL;var sw=Stopwatch.StartNew();
        for(var i=0;i<iterations;i++){value^=(uint)i;value*=1099511628211UL;value^=value>>17;}sw.Stop();GC.KeepAlive(value);
        return decimal.Round((decimal)iterations/Math.Max(0.001m,(decimal)sw.Elapsed.TotalSeconds)/1_000_000m,3);
    }
    private static decimal MeasureMultiCore()
    {
        var workers=Math.Max(1,Environment.ProcessorCount);const int perWorker=3_000_000;var sw=Stopwatch.StartNew();
        Parallel.For(0,workers,w=>{ulong value=(ulong)(w+1);for(var i=0;i<perWorker;i++){value=value*6364136223846793005UL+1442695040888963407UL;value^=value>>23;}GC.KeepAlive(value);});sw.Stop();
        return decimal.Round((decimal)(workers*perWorker)/Math.Max(0.001m,(decimal)sw.Elapsed.TotalSeconds)/1_000_000m,3);
    }
    private static decimal MeasureMemoryBandwidth()
    {
        const int bytes=64*1024*1024;var source=new byte[bytes];var target=new byte[bytes];Random.Shared.NextBytes(source);var sw=Stopwatch.StartNew();
        for(var i=0;i<8;i++)Buffer.BlockCopy(source,0,target,0,bytes);sw.Stop();GC.KeepAlive(target);
        var mb=(decimal)bytes*8m/1024m/1024m;return decimal.Round(mb/Math.Max(0.001m,(decimal)sw.Elapsed.TotalSeconds),3);
    }
    private static async Task<decimal> MeasureStorageAsync(string dir,CancellationToken ct)
    {
        const int bytes=32*1024*1024;var data=new byte[1024*1024];Random.Shared.NextBytes(data);var file=Path.Combine(dir,"storage-calibration.tmp");var sw=Stopwatch.StartNew();
        await using(var stream=new FileStream(file,FileMode.Create,FileAccess.Write,FileShare.None,1024*1024,FileOptions.Asynchronous|FileOptions.WriteThrough))for(var i=0;i<32;i++)await stream.WriteAsync(data,ct);
        await using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.Asynchronous|FileOptions.SequentialScan)){var buffer=new byte[data.Length];while(await stream.ReadAsync(buffer,ct)>0){}}
        sw.Stop();try{File.Delete(file);}catch{}
        return decimal.Round((bytes*2m/1024m/1024m)/Math.Max(0.001m,(decimal)sw.Elapsed.TotalSeconds),3);
    }
    private async Task<(decimal score,string? adapter,bool available)> MeasureGpuAsync(string dir,CancellationToken ct)
    {
        if(!OperatingSystem.IsWindows())return(0m,null,false);var configured=qaOptions.Value.HardwareProbePath;var path=Path.IsPathRooted(configured)?configured:Path.GetFullPath(configured);if(!File.Exists(path))return(0m,null,false);
        var result=await executor.RunAsync(path,["--json"],Environment.CurrentDirectory,null,TimeSpan.FromSeconds(30),Path.Combine(dir,"gpu-probe.stdout.log"),Path.Combine(dir,"gpu-probe.stderr.log"),ct);
        try{using var doc=JsonDocument.Parse(result.StandardOutput);var root=doc.RootElement;return(root.TryGetProperty("gpuScore",out var score)&&score.TryGetDecimal(out var n)?n:0m,root.TryGetProperty("adapter",out var a)?a.GetString():null,result.ExitCode==0);}catch{return(0m,null,false);}
    }
    private static long GetAvailableMemoryMb()
    {
        if(OperatingSystem.IsWindows()){var status=new MEMORYSTATUSEX{dwLength=(uint)Marshal.SizeOf<MEMORYSTATUSEX>()};if(GlobalMemoryStatusEx(ref status))return(long)(status.ullAvailPhys/1024/1024);}
        var bytes=GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;return bytes>0?bytes/1024/1024:0;
    }
    [StructLayout(LayoutKind.Sequential)]private struct MEMORYSTATUSEX{public uint dwLength;public uint dwMemoryLoad;public ulong ullTotalPhys;public ulong ullAvailPhys;public ulong ullTotalPageFile;public ulong ullAvailPageFile;public ulong ullTotalVirtual;public ulong ullAvailVirtual;public ulong ullAvailExtendedVirtual;}
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
