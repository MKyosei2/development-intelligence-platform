// Purpose: Builds a minimal QA process environment and blocks secret-bearing or unsafe injection variables.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dip.Application.Abstractions.Observability;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

internal static class ProcessEnvironmentSanitizer
{
    internal static readonly HashSet<string> UnsafeEnvironmentNames = new(
        new[]
        {
            "LD_PRELOAD","LD_LIBRARY_PATH","PYTHONPATH","PYTHONHOME","NODE_OPTIONS","DOTNET_STARTUP_HOOKS",
            "DOTNET_ADDITIONAL_DEPS","MSBuildSDKsPath","BASH_ENV","ENV","PROMPT_COMMAND","RUBYOPT","PERL5OPT",
            "JAVA_TOOL_OPTIONS","_JAVA_OPTIONS","COR_ENABLE_PROFILING","CORECLR_ENABLE_PROFILING",
            "COMPLUS_ProfAPI_Profiler","COMPLUS_Profiler","CORECLR_PROFILER","GIT_CONFIG_GLOBAL","GIT_CONFIG_SYSTEM"
        },
        StringComparer.OrdinalIgnoreCase);

    internal static void Sanitize(
            IDictionary<string, string?> target,
            IReadOnlyDictionary<string, string>? supplied)
    {
        var source = target.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        target.Clear();
        foreach (var key in new[]
                 {
                         "PATH","SystemRoot","WINDIR","HOME","USERPROFILE","TEMP","TMP","TMPDIR",
                         "DOTNET_ROOT","DOTNET_ROOT_X64","JAVA_HOME","GOPATH","GOROOT","NUGET_PACKAGES"
                     })
        {
            if (source.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                target[key] = value;
        }

        if (supplied is not null)
        {
            foreach (var pair in supplied)
            {
                if (!IsSecretName(pair.Key) && !IsUnsafeEnvironmentName(pair.Key))
                    target[pair.Key] = pair.Value;
            }
        }
        target["DIP_QA_SANDBOX"] = "1";
    }

    internal static bool IsSecretName(string key)
            => key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
               || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
               || key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
               || key.Contains("API_KEY", StringComparison.OrdinalIgnoreCase)
               || key.Contains("CONNECTIONSTRING", StringComparison.OrdinalIgnoreCase);

    internal static bool IsUnsafeEnvironmentName(string key)
            => UnsafeEnvironmentNames.Contains(key)
               || key.StartsWith("DYLD_", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("GIT_CONFIG_", StringComparison.OrdinalIgnoreCase);
}
