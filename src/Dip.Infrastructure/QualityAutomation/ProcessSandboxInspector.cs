// Purpose: Verifies externally supplied filesystem/network isolation and classifies sandbox denials.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dip.Application.Abstractions.Observability;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

internal static class ProcessSandboxInspector
{
    internal static bool FilesystemIsolationVerified()
            => string.Equals(
                Environment.GetEnvironmentVariable("DIP_QA_FILESYSTEM_ISOLATED"),
                "1",
                StringComparison.Ordinal);

    internal static bool NetworkIsolationVerified()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("DIP_QA_NETWORK_ISOLATED"),
                "1",
                StringComparison.Ordinal))
            return false;

        // On Linux the assertion is accepted only when the current namespace has no
        // non-loopback route. A plain environment variable inside a normally networked
        // worker is therefore insufficient to claim isolation.
        if (OperatingSystem.IsLinux())
            return LinuxHasOnlyLoopbackRoutes();

        // Windows isolation is supplied by the external sandbox/runner. The Windows
        // Job Object below controls CPU/memory/process-tree lifetime but is not itself
        // a network boundary, so the deployment must provide this explicit attestation.
        return true;
    }

    internal static bool LinuxHasOnlyLoopbackRoutes()
    {
        try
        {
            var observedRouteState = false;
            if (File.Exists("/proc/net/route"))
            {
                observedRouteState = true;
                foreach (var line in File.ReadLines("/proc/net/route").Skip(1))
                {
                    var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length > 0 && !columns[0].Equals("lo", StringComparison.Ordinal))
                        return false;
                }
            }

            if (File.Exists("/proc/net/ipv6_route"))
            {
                observedRouteState = true;
                foreach (var line in File.ReadLines("/proc/net/ipv6_route"))
                {
                    var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length > 0
                        && !columns[^1].Equals("lo", StringComparison.Ordinal))
                        return false;
                }
            }
            return observedRouteState;
        }
        catch
        {
            // An unverifiable namespace is not an isolated namespace.
            return false;
        }
    }

    internal static string ClassifySandboxDenial(string message)
            => message.Contains("network isolation", StringComparison.OrdinalIgnoreCase)
                ? "network-isolation"
                : message.Contains("workspace", StringComparison.OrdinalIgnoreCase)
                    ? "workspace-boundary"
                    : message.Contains("allowlist", StringComparison.OrdinalIgnoreCase)
                      || message.Contains("trusted PATH", StringComparison.OrdinalIgnoreCase)
                        ? "executable-policy"
                        : "sandbox-policy";
}
