// Purpose: Locks QA process path boundaries while allowing only canonical CMake toolchain paths that were independently resolved as trusted.
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class ProcessCommandValidatorTests
{
    [Theory]
    [InlineData("-DCMAKE_CXX_FLAGS=/W4 /fsanitize=address /Zi")]
    [InlineData("-DCMAKE_C_FLAGS_RELEASE=/O2")]
    [InlineData("-DCMAKE_EXE_LINKER_FLAGS=/DEBUG /INCREMENTAL:NO")]
    public void CMakeFlagAssignments_AreNotTreatedAsFilesystemPaths(string argument)
    {
        using var workspace = TemporaryDirectory.Create();

        var candidate = ProcessCommandValidator.ArgumentPathCandidate(
            argument,
            workspace.Path);

        candidate.Should().BeNull();
    }

    [Fact]
    public void OrdinaryRelativePathAssignment_CannotEscapeWorkspace()
    {
        using var workspace = TemporaryDirectory.Create();

        Action act = () => ProcessCommandValidator.ValidatePathArguments(
            new[] { "--output=../outside/result.json" },
            workspace.Path);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*escapes the workspace*");
    }

    [Fact]
    public void ExistingPathInsideWorkspace_IsAllowed()
    {
        using var workspace = TemporaryDirectory.Create();
        var inside = System.IO.Path.Combine(workspace.Path, "artifacts");
        Directory.CreateDirectory(inside);

        Action act = () => ProcessCommandValidator.ValidatePathArguments(
            new[] { inside },
            workspace.Path);

        act.Should().NotThrow();
    }

    [Fact]
    public void AbsolutePathOutsideWorkspace_IsRejected()
    {
        using var workspace = TemporaryDirectory.Create();
        var outside = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dip-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);

        try
        {
            Action act = () => ProcessCommandValidator.ValidatePathArguments(
                new[] { outside },
                workspace.Path);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*escapes the workspace*");
        }
        finally
        {
            try
            {
                Directory.Delete(outside, true);
            }
            catch
            {
                // Best-effort test cleanup only.
            }
        }
    }

    [Fact]
    public void VisualStudioGeneratorInstance_MustExactlyMatchTrustedInstallation()
    {
        using var root = TemporaryDirectory.Create();
        var expected = System.IO.Path.Combine(
            root.Path,
            "Microsoft Visual Studio",
            "18",
            "Community");
        var vcvars = System.IO.Path.Combine(
            expected,
            "VC",
            "Auxiliary",
            "Build",
            "vcvars64.bat");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(vcvars)!);
        File.WriteAllText(vcvars, "@echo off");

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        ProcessCommandValidator.IsTrustedVisualStudioInstance(
                expected,
                expected,
                comparison)
            .Should()
            .BeTrue();

        var sibling = System.IO.Path.Combine(root.Path, "untrusted");
        Directory.CreateDirectory(sibling);

        ProcessCommandValidator.IsTrustedVisualStudioInstance(
                sibling,
                expected,
                comparison)
            .Should()
            .BeFalse();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dip-validator-tests-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // Best-effort test cleanup only.
            }
        }
    }
}
