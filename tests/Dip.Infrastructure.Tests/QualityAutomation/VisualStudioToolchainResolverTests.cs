// Purpose: Locks Windows Visual Studio/MSVC discovery, environment capture scripting, and safe environment filtering used by C++ QA.
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class VisualStudioToolchainResolverTests
{
    [Theory]
    [InlineData("18.5.4", "Visual Studio 18 2026")]
    [InlineData("17.11.5", "Visual Studio 17 2022")]
    [InlineData("16.11.34", "Visual Studio 16 2019")]
    [InlineData("15.9.52", "Visual Studio 15 2017")]
    public void Visual_studio_version_maps_to_cmake_generator(string version, string expected)
    {
        VisualStudioToolchainResolver.TryMapGenerator(version).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("19.0")]
    public void Unsupported_or_invalid_visual_studio_version_has_no_generator(string version)
    {
        VisualStudioToolchainResolver.TryMapGenerator(version).Should().BeNull();
    }

    [Fact]
    public void Developer_environment_parser_forwards_only_required_toolchain_variables()
    {
        const string environment =
            "PATH=C:\\VS\\bin;C:\\Windows\\System32\r\n"
            + "INCLUDE=C:\\VS\\include\r\n"
            + "LIB=C:\\VS\\lib\r\n"
            + "LIBPATH=C:\\VS\\libpath\r\n"
            + "VCINSTALLDIR=C:\\VS\\VC\\\r\n"
            + "VCToolsInstallDir=C:\\VS\\VC\\Tools\\MSVC\\14.50\\\r\n"
            + "VSINSTALLDIR=C:\\VS\\\r\n"
            + "WindowsSdkDir=C:\\Program Files (x86)\\Windows Kits\\10\\\r\n"
            + "WindowsSDKVersion=10.0.26100.0\\\r\n"
            + "VSCMD_ARG_HOST_ARCH=x64\r\n"
            + "VSCMD_ARG_TGT_ARCH=x64\r\n"
            + "SOME_UNRELATED_VALUE=do-not-forward\r\n"
            + "MY_TOKEN=must-not-forward\r\n";

        var parsed = VisualStudioToolchainResolver.ParseDeveloperEnvironment(environment);

        parsed.Should().ContainKey("PATH");
        parsed.Should().ContainKey("INCLUDE");
        parsed.Should().ContainKey("LIB");
        parsed.Should().ContainKey("LIBPATH");
        parsed.Should().ContainKey("VCINSTALLDIR");
        parsed.Should().ContainKey("VCToolsInstallDir");
        parsed.Should().ContainKey("VSINSTALLDIR");
        parsed.Should().ContainKey("WindowsSdkDir");
        parsed.Should().ContainKey("WindowsSDKVersion");
        parsed.Should().ContainKey("VSCMD_ARG_HOST_ARCH");
        parsed.Should().ContainKey("VSCMD_ARG_TGT_ARCH");
        parsed.Should().NotContainKey("SOME_UNRELATED_VALUE");
        parsed.Should().NotContainKey("MY_TOKEN");
    }

    [Fact]
    public void Capture_script_calls_quoted_vcvars_and_emits_environment_only_after_success()
    {
        const string vcvars = @"C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat";

        var script = VisualStudioToolchainResolver.BuildDeveloperEnvironmentCaptureScript(vcvars);

        script.Should().Contain($"call \"{vcvars}\" >nul");
        script.Should().Contain("set \"_DIP_VCVARS_EXIT=%ERRORLEVEL%\"");
        script.Should().Contain("  exit /b %_DIP_VCVARS_EXIT%");
        script[^1].Should().Be("set");
    }

    [Fact]
    public void Compiler_resolver_prefers_vctools_hostx64_x64_location()
    {
        var root = Path.Combine(Path.GetTempPath(), "dip-vctools-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var compiler = Path.Combine(root, "bin", "Hostx64", "x64", "cl.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(compiler)!);
            File.WriteAllText(compiler, string.Empty);

            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["VCToolsInstallDir"] = root,
                ["PATH"] = string.Empty
            };

            VisualStudioToolchainResolver.ResolveCompilerPath(environment)
                .Should().Be(Path.GetFullPath(compiler));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compiler_resolver_falls_back_to_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "dip-cl-path-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var compiler = Path.Combine(root, "cl.exe");
            File.WriteAllText(compiler, string.Empty);

            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = root
            };

            VisualStudioToolchainResolver.ResolveCompilerPath(environment)
                .Should().Be(Path.GetFullPath(compiler));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
