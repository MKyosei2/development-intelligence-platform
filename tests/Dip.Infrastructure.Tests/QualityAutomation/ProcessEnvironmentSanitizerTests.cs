// Purpose: Verifies the QA sandbox preserves required non-secret Windows toolchain variables while removing secrets and injection variables.
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class ProcessEnvironmentSanitizerTests
{
    [Fact]
    public void Sanitize_PreservesRequiredNonSecretToolchainEnvironment()
    {
        var target = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = @"C:\Tools",
            ["PATHEXT"] = ".COM;.EXE;.BAT;.CMD",
            ["ComSpec"] = @"C:\Windows\System32\cmd.exe",
            ["APPDATA"] = @"C:\Users\Test\AppData\Roaming",
            ["LOCALAPPDATA"] = @"C:\Users\Test\AppData\Local",
            ["ProgramData"] = @"C:\ProgramData",
            ["ProgramFiles"] = @"C:\Program Files",
            ["ProgramFiles(x86)"] = @"C:\Program Files (x86)",
            ["ProgramW6432"] = @"C:\Program Files"
        };

        ProcessEnvironmentSanitizer.Sanitize(
            target,
            supplied: null);

        foreach (var key in new[]
                 {
                     "PATH",
                     "PATHEXT",
                     "ComSpec",
                     "APPDATA",
                     "LOCALAPPDATA",
                     "ProgramData",
                     "ProgramFiles",
                     "ProgramFiles(x86)",
                     "ProgramW6432"
                 })
        {
            Assert.True(
                target.ContainsKey(key),
                $"Expected safe inherited environment variable '{key}' to be preserved.");
        }

        Assert.Equal("1", target["DIP_QA_SANDBOX"]);
    }

    [Fact]
    public void Sanitize_RemovesSecretsAndUnsafeInjectionVariables()
    {
        var target = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = @"C:\Tools",
            ["LD_PRELOAD"] = "malicious.so",
            ["PYTHONPATH"] = @"C:\Injected",
            ["DOTNET_STARTUP_HOOKS"] = @"C:\Injected\hook.dll"
        };

        var supplied = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["SAFE_FLAG"] = "1",
            ["API_KEY"] = "not-a-real-key",
            ["SERVICE_TOKEN"] = "not-a-real-token",
            ["DATABASE_PASSWORD"] = "not-a-real-password",
            ["NODE_OPTIONS"] = "--require injected.js"
        };

        ProcessEnvironmentSanitizer.Sanitize(
            target,
            supplied);

        Assert.Equal("1", target["SAFE_FLAG"]);
        Assert.Equal("1", target["DIP_QA_SANDBOX"]);

        foreach (var forbidden in new[]
                 {
                     "LD_PRELOAD",
                     "PYTHONPATH",
                     "DOTNET_STARTUP_HOOKS",
                     "API_KEY",
                     "SERVICE_TOKEN",
                     "DATABASE_PASSWORD",
                     "NODE_OPTIONS"
                 })
        {
            Assert.False(
                target.ContainsKey(forbidden),
                $"Unsafe or secret environment variable '{forbidden}' must not reach the QA process.");
        }
    }
}
