// Purpose: Defines configuration options for Q aE xe cu ti on Op ti on s.
namespace Dip.Infrastructure.QualityAutomation;

public sealed class QaExecutionOptions
{
    public const string SectionName="Qa";
    public string WorkspaceRoot { get; set; } = ".";
    public string ArtifactRoot { get; set; } = "artifacts/qa";
    public string CaptureAgentPath { get; set; } = "collectors/capture-agent/build/Release/dip-capture-agent.exe";
    public string CrashWatchdogPath { get; set; } = "collectors/crash-watchdog/build/Release/dip-crash-watchdog.exe";
    public string HardwareProbePath { get; set; } = "collectors/hardware-probe/build/Release/dip-hardware-probe.exe";
    public string HardwareStressAgentPath { get; set; } = "collectors/hardware-stress-agent/build/Release/dip-hardware-stress-agent.exe";
    public int DefaultTimeoutSeconds { get; set; } = 120;
    public int MaximumOutputCharacters { get; set; } = 2_000_000;
    public bool SandboxEnabled { get; set; } = true;
    public int SandboxMemoryLimitMb { get; set; } = 4096;
    public int SandboxCpuPercent { get; set; } = 90;
    public bool SanitizeEnvironment { get; set; } = true;
    public bool RequireExternalNetworkIsolation { get; set; } = false;
    public bool RequireExternalFilesystemIsolation { get; set; } = false;
}
