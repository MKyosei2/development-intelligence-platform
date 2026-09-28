// Purpose: Implements Environment Snapshot Service infrastructure behavior for quality automation.
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class EnvironmentSnapshotService(ProcessExecutor executor)
{
    public async Task<string> CaptureAsync(string workspace, string executable, string outputPath, CancellationToken ct)
    {
        var gpu = await TryReadGpuAsync(workspace, ct);
        var snapshot = new
        {
            capturedAtUtc = DateTimeOffset.UtcNow,
            os = RuntimeInformation.OSDescription,
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            framework = RuntimeInformation.FrameworkDescription,
            machine = Environment.MachineName,
            processorCount = Environment.ProcessorCount,
            totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            workingDirectory = workspace,
            executable,
            executableSha256 = HashFile(executable),
            gpu,
            environment = new
            {
                CI = Environment.GetEnvironmentVariable("CI"),
                DOTNET_ENVIRONMENT = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
                PYTHONHASHSEED = Environment.GetEnvironmentVariable("PYTHONHASHSEED")
            }
        };
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!); await File.WriteAllTextAsync(outputPath, json, ct); return json;
    }

    private async Task<string?> TryReadGpuAsync(string workspace, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var powershell = ProcessExecutor.FindExecutable("powershell") ?? ProcessExecutor.FindExecutable("pwsh"); if (powershell is null) return null;
        var tmp = Path.Combine(Path.GetTempPath(), $"dip-gpu-{Guid.NewGuid():N}"); Directory.CreateDirectory(tmp);
        var result = await executor.RunAsync(powershell, ["-NoProfile", "-Command", "Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name"], workspace, null, TimeSpan.FromSeconds(10), Path.Combine(tmp, "out.log"), Path.Combine(tmp, "err.log"), ct);
        try { Directory.Delete(tmp, true); }
        catch
        {
            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
        }
        return result.ExitCode == 0 ? string.Join("; ", result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Distinct()) : null;
    }
    private static string? HashFile(string executable)
    {
        try
        {
            if (!File.Exists(executable)) return null;
            using var stream = File.OpenRead(executable);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch
        {
            return null;
        }
    }
}
