// Purpose: Verifies Git Branch QA Workspace Manager behavior, edge cases, and repository invariants.
using System.Diagnostics;
using System.Text.Json;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHubIntegration;
using Dip.Application.Features.QualityAutomation;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class GitBranchQaWorkspaceManagerTests
{
    [Fact]
    public async Task BranchWorkspace_IsIsolated_AndSharedCheckoutIsUnchanged()
    {
        if (!GitAvailable()) return;
        var root = Path.Combine(Path.GetTempPath(), "dip-branch-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Run(root, "init", "-b", "main");
            Run(root, "config", "user.email", "dip-tests@example.invalid");
            Run(root, "config", "user.name", "DIP Tests");
            await File.WriteAllTextAsync(Path.Combine(root, "value.txt"), "main");
            Run(root, "add", "value.txt"); Run(root, "commit", "-m", "main");
            Run(root, "checkout", "-b", "feature/branch-qa");
            await File.WriteAllTextAsync(Path.Combine(root, "value.txt"), "feature");
            Run(root, "commit", "-am", "feature");
            Run(root, "checkout", "main");

            var manager = new GitBranchQaWorkspaceManager();
            var catalog = await manager.ListBranchesAsync(root, "main", false, CancellationToken.None);
            catalog.Branches.Should().Contain(x => x.Branch == "feature/branch-qa");

            string isolatedPath;
            await using (var lease = await manager.PrepareAsync(root, ".", "feature/branch-qa", null, false, Guid.NewGuid(), CancellationToken.None))
            {
                isolatedPath = lease.WorkspacePath;
                lease.IsIsolated.Should().BeTrue();
                Directory.Exists(isolatedPath).Should().BeTrue();
                (await File.ReadAllTextAsync(Path.Combine(isolatedPath, "value.txt"))).Should().Be("feature");
                Run(root, "branch", "--show-current").Trim().Should().Be("main");
                (await File.ReadAllTextAsync(Path.Combine(root, "value.txt"))).Should().Be("main");
            }
            Directory.Exists(isolatedPath).Should().BeFalse();
            Run(root, "branch", "--show-current").Trim().Should().Be("main");
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch
            {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }
    }


    [Fact]
    public async Task ArchiveWorkspace_UsesAuthenticatedIsolatedBranchSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), "dip-archive-branch-test-" + Guid.NewGuid().ToString("N"));
        var projectId = Guid.NewGuid();
        var relative = $"github/{projectId:N}";
        var source = Path.Combine(root, "github", projectId.ToString("N"));
        Directory.CreateDirectory(source);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, "value.txt"), "main");
            var marker = new GitHubWorkspaceSyncDto(projectId, "owner/repo", "main", relative, new string('a', 40), 1, 4, DateTimeOffset.UtcNow);
            await File.WriteAllTextAsync(Path.Combine(source, ".dip-sync.json"), JsonSerializer.Serialize(marker, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var catalog = new FakeGitHubCatalog(root);
            var manager = new GitBranchQaWorkspaceManager(catalog);

            var branches = await manager.ListBranchesAsync(source, "main", false, CancellationToken.None);
            branches.Branches.Should().Contain(x => x.Branch == "feature/archive");

            string isolated;
            await using (var lease = await manager.PrepareAsync(source, relative, "feature/archive", null, true, Guid.NewGuid(), CancellationToken.None))
            {
                isolated = lease.WorkspacePath;
                lease.IsIsolated.Should().BeTrue();
                lease.RefreshStatus.Should().Be("GitHubArchive");
                lease.BranchName.Should().Be("feature/archive");
                (await File.ReadAllTextAsync(Path.Combine(isolated, "value.txt"))).Should().Be("feature/archive");
                (await File.ReadAllTextAsync(Path.Combine(source, "value.txt"))).Should().Be("main");
            }
            Directory.Exists(isolated).Should().BeFalse();
            (await File.ReadAllTextAsync(Path.Combine(source, "value.txt"))).Should().Be("main");
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch
            {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }
    }

    private sealed class FakeGitHubCatalog(string workspaceRoot) : IGitHubRepositoryCatalog
    {
        public Task<GitHubConnectionStatusDto> GetStatusAsync(CancellationToken ct) => Task.FromResult(new GitHubConnectionStatusDto(true, true, "Test", null, "owner", null, null, "ok"));
        public Task<IReadOnlyList<GitHubRepositorySummaryDto>> ListRepositoriesAsync(string? search, string? visibility, CancellationToken ct) => Task.FromResult<IReadOnlyList<GitHubRepositorySummaryDto>>(Array.Empty<GitHubRepositorySummaryDto>());
        public Task<GitHubRepositoryDetailsDto> GetRepositoryAsync(string owner, string repository, CancellationToken ct)
        {
            var summary = new GitHubRepositorySummaryDto(1, owner, repository, $"{owner}/{repository}", "https://github.com/owner/repo", "main", true, false, "C#", null, 0, 0, 0, DateTimeOffset.UtcNow);
            var runs = new[] { new GitHubWorkflowRunDto(1, "CI", "push", "completed", "success", "feature/archive", new string('b', 40), "https://example.invalid", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) };
            return Task.FromResult(new GitHubRepositoryDetailsDto(summary, new string('a', 40), "main", DateTimeOffset.UtcNow, runs));
        }
        public Task<IReadOnlyList<GitHubPullRequestSnapshotDto>> ListPullRequestsAsync(string owner, string repository, string state, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GitHubPullRequestSnapshotDto>>(new[] { new GitHubPullRequestSnapshotDto(7, "branch", "open", new string('b', 40), "feature/archive", "https://example.invalid", false, new[] { new string('b', 40) }, Array.Empty<GitHubPullRequestFileDto>(), Array.Empty<GitHubPullRequestReviewDto>()) });
        public async Task<GitHubWorkspaceSyncDto> SyncWorkspaceAsync(Guid projectId, string owner, string repository, string branch, CancellationToken ct)
        {
            var relative = $"github/{projectId:N}";
            var target = Path.Combine(workspaceRoot, "github", projectId.ToString("N"));
            Directory.CreateDirectory(target);
            await File.WriteAllTextAsync(Path.Combine(target, "value.txt"), branch, ct);
            return new GitHubWorkspaceSyncDto(projectId, $"{owner}/{repository}", branch, relative, new string('b', 40), 1, branch.Length, DateTimeOffset.UtcNow);
        }
        public Task<GitHubWorkspaceSyncDto?> GetWorkspaceAsync(Guid projectId, CancellationToken ct) => Task.FromResult<GitHubWorkspaceSyncDto?>(null);
    }

    private static bool GitAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", "--version") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            p?.WaitForExit(3000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string Run(string cwd, params string[] args)
    {
        using var p = new Process(); p.StartInfo = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
        p.Start(); var output = p.StandardOutput.ReadToEnd(); var error = p.StandardError.ReadToEnd(); p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(" ", args)} failed: {error}");
        return output;
    }
}
