// Purpose: Resolves an installed Visual Studio C++ toolchain and captures the minimum developer environment required by sandboxed CMake/MSBuild QA processes.
using System.Diagnostics;

namespace Dip.Infrastructure.QualityAutomation;

internal sealed record VisualStudioToolchain(
    string Generator,
    string InstallationPath,
    string DeveloperEnvironmentScript,
    IReadOnlyDictionary<string, string> Environment,
    bool DeveloperEnvironmentResolved,
    int? DeveloperEnvironmentExitCode,
    string DeveloperEnvironmentDetail,
    string? CompilerPath,
    bool CompilerExists);

internal static class VisualStudioToolchainResolver
{
    private static readonly string[] ForwardedEnvironmentNames =
    [
        "PATH",
        "INCLUDE",
        "LIB",
        "LIBPATH",
        "VCINSTALLDIR",
        "VCToolsInstallDir",
        "VCToolsRedistDir",
        "VSINSTALLDIR",
        "VisualStudioVersion",
        "DevEnvDir",
        "ExtensionSdkDir",
        "FrameworkDir",
        "FrameworkDir64",
        "FrameworkVersion",
        "FrameworkVersion64",
        "NETFXSDKDir",
        "UniversalCRTSdkDir",
        "UCRTVersion",
        "WindowsLibPath",
        "WindowsSdkBinPath",
        "WindowsSdkDir",
        "WindowsSdkExecutablePath_x64",
        "WindowsSDKVersion",
        "WindowsSdkVerBinPath",
        "VSCMD_ARG_HOST_ARCH",
        "VSCMD_ARG_TGT_ARCH",
        "VSCMD_VER"
    ];

    internal static VisualStudioToolchain? TryResolve()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var vswhere = TryResolveVsWherePath();
        if (vswhere is null)
            return null;

        var installationPath = RunVsWhereProperty(vswhere, "installationPath");
        var installationVersion = RunVsWhereProperty(vswhere, "installationVersion");
        var generator = TryMapGenerator(installationVersion);

        if (string.IsNullOrWhiteSpace(installationPath) || generator is null)
            return null;

        var vcvars64 = Path.Combine(
            installationPath,
            "VC",
            "Auxiliary",
            "Build",
            "vcvars64.bat");

        if (!File.Exists(vcvars64))
        {
            return new(
                generator,
                installationPath,
                vcvars64,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                false,
                null,
                "vcvars64.bat was not found in the resolved Visual Studio installation.",
                null,
                false);
        }

        var capture = CaptureDeveloperEnvironment(vcvars64);
        return new(
            generator,
            installationPath,
            vcvars64,
            capture.Environment,
            capture.Resolved,
            capture.ExitCode,
            capture.Detail,
            capture.CompilerPath,
            capture.CompilerPath is not null && File.Exists(capture.CompilerPath));
    }

    internal static string? TryMapGenerator(string? installationVersion)
    {
        if (string.IsNullOrWhiteSpace(installationVersion))
            return null;

        var firstDot = installationVersion.IndexOf('.');
        var majorText = firstDot >= 0
            ? installationVersion[..firstDot]
            : installationVersion;

        if (!int.TryParse(majorText.Trim(), out var major))
            return null;

        return major switch
        {
            18 => "Visual Studio 18 2026",
            17 => "Visual Studio 17 2022",
            16 => "Visual Studio 16 2019",
            15 => "Visual Studio 15 2017",
            _ => null
        };
    }

    internal static IReadOnlyDictionary<string, string> ParseDeveloperEnvironment(string output)
    {
        var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = rawLine.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = rawLine[..separator].Trim();
            var value = rawLine[(separator + 1)..];
            if (key.Length == 0 || value.Length == 0)
                continue;

            all[key] = value;
        }

        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ForwardedEnvironmentNames)
        {
            if (all.TryGetValue(name, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                selected[name] = value;
            }
        }

        return selected;
    }

    internal static string? ResolveCompilerPath(IReadOnlyDictionary<string, string> environment)
    {
        if (environment.TryGetValue("VCToolsInstallDir", out var toolsRoot)
            && !string.IsNullOrWhiteSpace(toolsRoot))
        {
            var candidate = Path.Combine(
                toolsRoot.Trim().Trim('"'),
                "bin",
                "Hostx64",
                "x64",
                "cl.exe");
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        if (!environment.TryGetValue("PATH", out var pathValue)
            || string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var rawSegment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var segment = rawSegment.Trim().Trim('"');
                if (segment.Length == 0)
                    continue;

                var candidate = Path.Combine(segment, "cl.exe");
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch
            {
                // Ignore malformed PATH segments and continue searching the developer environment.
            }
        }

        return null;
    }

    internal static string[] BuildDeveloperEnvironmentCaptureScript(string vcvars64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vcvars64);
        var escaped = vcvars64.Replace("\"", "\"\"");
        return
        [
            "@echo off",
            $"call \"{escaped}\" >nul",
            "set \"_DIP_VCVARS_EXIT=%ERRORLEVEL%\"",
            "if not \"%_DIP_VCVARS_EXIT%\"==\"0\" (",
            "  echo vcvars64.bat exited with code %_DIP_VCVARS_EXIT%. 1>&2",
            "  exit /b %_DIP_VCVARS_EXIT%",
            ")",
            "set"
        ];
    }

    private static string? TryResolveVsWherePath()
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrWhiteSpace(programFilesX86))
            return null;

        var candidate = Path.Combine(
            programFilesX86,
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");

        return File.Exists(candidate) ? candidate : null;
    }

    private static string? RunVsWhereProperty(string vswhere, string property)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = vswhere,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in new[]
                     {
                         "-latest",
                         "-products", "*",
                         "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                         "-property", property
                     })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var stdout = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0
                ? stdout.Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static DeveloperEnvironmentCapture CaptureDeveloperEnvironment(string vcvars64)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "dip-vsenv-" + Guid.NewGuid().ToString("N"));

        try
        {
            var comSpec = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(comSpec) || !File.Exists(comSpec))
                comSpec = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

            if (string.IsNullOrWhiteSpace(comSpec) || !File.Exists(comSpec))
            {
                return DeveloperEnvironmentCapture.Failure(
                    null,
                    "cmd.exe could not be resolved from ComSpec or the Windows system directory.");
            }

            Directory.CreateDirectory(tempDirectory);
            const string scriptFileName = "capture-vsenv.cmd";
            var scriptPath = Path.Combine(tempDirectory, scriptFileName);
            File.WriteAllLines(scriptPath, BuildDeveloperEnvironmentCaptureScript(vcvars64));

            var psi = new ProcessStartInfo
            {
                FileName = comSpec,
                WorkingDirectory = tempDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Keep the /c payload free of spaces/quotes by running a fixed script name
            // from its working directory. cmd.exe has special /s and /c quote-stripping
            // semantics; passing `call "C:\Program Files\...\vcvars64.bat"` directly
            // through ProcessStartInfo.ArgumentList is not reliable across Windows/.NET
            // versions and was the cause of the runtime-only toolchain capture failure.
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(scriptFileName);

            using var process = Process.Start(psi);
            if (process is null)
                return DeveloperEnvironmentCapture.Failure(null, "cmd.exe failed to start.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(30_000))
            {
                try { process.Kill(entireProcessTree: true); }
                catch
                {
                    // Best effort only; timeout is already the authoritative failure.
                }
                return DeveloperEnvironmentCapture.Failure(null, "vcvars64.bat environment capture timed out after 30 seconds.");
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            var exitCode = process.ExitCode;

            if (exitCode != 0)
            {
                return DeveloperEnvironmentCapture.Failure(
                    exitCode,
                    BuildFailureDetail($"vcvars64.bat exited with code {exitCode}.", stderr));
            }

            var environment = ParseDeveloperEnvironment(stdout);
            var compilerPath = ResolveCompilerPath(environment);
            var requiredVariablesPresent =
                environment.ContainsKey("PATH")
                && environment.ContainsKey("INCLUDE")
                && environment.ContainsKey("LIB")
                && environment.ContainsKey("VCToolsInstallDir");

            if (!requiredVariablesPresent)
            {
                return new(
                    environment,
                    false,
                    exitCode,
                    BuildFailureDetail(
                        "vcvars64.bat exited successfully, but one or more required MSVC variables (PATH, INCLUDE, LIB, VCToolsInstallDir) were missing.",
                        stderr),
                    compilerPath);
            }

            if (compilerPath is null)
            {
                return new(
                    environment,
                    false,
                    exitCode,
                    BuildFailureDetail(
                        "vcvars64.bat exited successfully, but cl.exe could not be resolved from VCToolsInstallDir or PATH.",
                        stderr),
                    null);
            }

            return new(
                environment,
                true,
                exitCode,
                "vcvars64.bat completed successfully and cl.exe was resolved.",
                compilerPath);
        }
        catch (Exception ex)
        {
            return DeveloperEnvironmentCapture.Failure(
                null,
                $"Developer environment capture threw {ex.GetType().Name}: {LimitDetail(ex.Message)}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Temporary cleanup must not replace the real toolchain diagnostic.
            }
        }
    }

    private static string BuildFailureDetail(string prefix, string stderr)
    {
        var detail = string.Join(
            " | ",
            stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Take(8));

        return detail.Length == 0
            ? prefix
            : $"{prefix} {LimitDetail(detail)}";
    }

    private static string LimitDetail(string value)
        => value.Length <= 1200 ? value : value[..1200] + "...";

    private sealed record DeveloperEnvironmentCapture(
        IReadOnlyDictionary<string, string> Environment,
        bool Resolved,
        int? ExitCode,
        string Detail,
        string? CompilerPath)
    {
        internal static DeveloperEnvironmentCapture Failure(int? exitCode, string detail)
            => new(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                false,
                exitCode,
                detail,
                null);
    }
}
