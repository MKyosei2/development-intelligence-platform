// Purpose: Applies Windows Job Object CPU, memory, and process-tree lifetime constraints for sandboxed QA tools.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dip.Application.Abstractions.Observability;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

internal static class ProcessResourceLimiter
{
    internal static bool ApplyCpuLimit(IntPtr job, int percent)
    {
        var info = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
        {
            ControlFlags = 0x1 | 0x4,
            CpuRate = (uint)(Math.Clamp(percent, 1, 100) * 100)
        };
        return SetJobInfo(job, 15, info);
    }

    internal static bool ApplyMemoryAndKillOnClose(IntPtr job, int memoryMb)
    {
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = 0x2000;
        if (memoryMb > 0)
        {
            info.BasicLimitInformation.LimitFlags |= 0x100;
            info.ProcessMemoryLimit = (UIntPtr)((ulong)memoryMb * 1024UL * 1024UL);
        }
        var applied = SetJobInfo(job, 9, info);
        return applied && memoryMb > 0;
    }

    private static bool SetJobInfo<T>(IntPtr job, int cls, T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, false);
            return SetInformationJobObject(job, cls, ptr, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
    {
        public uint ControlFlags;
        public uint CpuRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll")]
    internal static extern bool CloseHandle(IntPtr h);
}
