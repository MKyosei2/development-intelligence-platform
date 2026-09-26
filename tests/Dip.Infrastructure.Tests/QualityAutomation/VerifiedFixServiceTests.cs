// Purpose: Verifies Verified Fix rejects unsafe, ungrounded, oversized, protected, and test-only proposals before execution.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class VerifiedFixServiceTests
{
    [Fact]
    public async Task VerifyAsync_RejectsPatchWithoutRegressionTests()
    {
        using var fixture = new Fixture();
        var result = await fixture.Service.VerifyAsync(
            Request(
                "diff --git a/src/a.cs b/src/a.cs\n--- a/src/a.cs\n+++ b/src/a.cs\n@@ -1 +1 @@\n-old\n+new\n",
                []),
            CancellationToken.None);

        result.Verified.Should().BeFalse();
        result.Status.Should().Be("Failed");
        result.Summary.Should().Contain("1-20 regression-test drafts");
    }

    [Theory]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData(".env")]
    [InlineData("config/secrets.json")]
    [InlineData("src/appsettings.production.json")]
    public async Task VerifyAsync_RejectsProtectedPaths(string path)
    {
        using var fixture = new Fixture();
        var diff = $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n-old\n+new\n";

        var result = await fixture.Service.VerifyAsync(
            Request(diff, [new("tests/regression.cs", "// regression", "Regression")]),
            CancellationToken.None);

        result.Verified.Should().BeFalse();
        result.Summary.Should().Contain("protected path");
    }

    [Fact]
    public async Task VerifyAsync_RejectsTestOnlyPatch()
    {
        using var fixture = new Fixture();
        var diff =
            "diff --git a/tests/a.cs b/tests/a.cs\n"
            + "--- a/tests/a.cs\n"
            + "+++ b/tests/a.cs\n"
            + "@@ -1 +1 @@\n"
            + "-old\n"
            + "+new\n";

        var result = await fixture.Service.VerifyAsync(
            Request(diff, [new("tests/regression.cs", "// regression", "Regression")]),
            CancellationToken.None);

        result.Verified.Should().BeFalse();
        result.Summary.Should().Contain("non-test source/configuration change");
    }

    [Fact]
    public async Task VerifyAsync_RejectsSuppressionTokens()
    {
        using var fixture = new Fixture();
        var diff =
            "diff --git a/src/a.cs b/src/a.cs\n"
            + "--- a/src/a.cs\n"
            + "+++ b/src/a.cs\n"
            + "@@ -1 +1 @@\n"
            + "-old\n"
            + "+#pragma warning disable\n";

        var result = await fixture.Service.VerifyAsync(
            Request(diff, [new("tests/regression.cs", "// regression", "Regression")]),
            CancellationToken.None);

        result.Verified.Should().BeFalse();
        result.Summary.Should().Contain("suppression token");
    }

    [Fact]
    public async Task VerifyAsync_RejectsOversizedPatch()
    {
        using var fixture = new Fixture();
        var oversized = "diff --git a/src/a.cs b/src/a.cs\n"
            + new string('x', 2_000_001);

        var result = await fixture.Service.VerifyAsync(
            Request(oversized, [new("tests/regression.cs", "// regression", "Regression")]),
            CancellationToken.None);

        result.Verified.Should().BeFalse();
        result.Summary.Should().Contain("2 MB");
    }

    private static VerifiedFixRequest Request(
        string diff,
        IReadOnlyList<RegressionTestDraft> tests) =>
        new(
            Guid.NewGuid(),
            ".",
            null,
            diff,
            tests,
            LanguageId.CSharp,
            30);

    private sealed class Fixture : IDisposable
    {
        private readonly string _workspace = Path.Combine(
            Path.GetTempPath(),
            "dip-verified-fix-tests",
            Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Directory.CreateDirectory(_workspace);
            Service = new VerifiedFixService(
                new WorkspaceResolver(_workspace),
                new Detector(),
                new Registry());
        }

        public VerifiedFixService Service { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_workspace))
                    Directory.Delete(_workspace, true);
            }
            catch
            {
                // Test cleanup is best effort; the service tests reject before spawning tools.
            }
        }
    }

    private sealed class WorkspaceResolver(string workspace) : IQaWorkspaceResolver
    {
        public string Resolve(string workspaceRelativePath) => workspace;
        public string QaArtifactRoot(Guid projectId, Guid qaRunId) =>
            Path.Combine(workspace, "artifacts", projectId.ToString("N"), qaRunId.ToString("N"));
    }

    private sealed class Detector : IRepositoryTechnologyDetector
    {
        public Task<QaTechnologyProfile> DetectAsync(string workspace, CancellationToken cancellationToken) =>
            Task.FromResult(new QaTechnologyProfile(
                workspace,
                [LanguageId.CSharp],
                [],
                [],
                [],
                []));
    }

    private sealed class Registry : IQaAdapterRegistry
    {
        public IReadOnlyList<string> AdapterIds => [];
        public IReadOnlyList<QaAdapterSelection> Resolve(
            LanguageId requested,
            QaTechnologyProfile profile,
            string workspace) => [];
    }
}
