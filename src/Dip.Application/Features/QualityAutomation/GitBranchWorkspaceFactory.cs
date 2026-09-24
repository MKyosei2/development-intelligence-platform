// Purpose: Creates and cleans isolated Git worktrees or authenticated GitHub archive snapshots for branch QA runs.
using Dip.Application.Abstractions.GitHub;

namespace Dip.Application.Features.QualityAutomation;

internal sealed class GitBranchWorkspaceFactory
{
    private readonly IGitHubRepositoryCatalog? _github;

    internal GitBranchWorkspaceFactory(IGitHubRepositoryCatalog? github) =>
        _github = github;

    internal async Task<BranchQaWorkspaceLease> PrepareAsync(
        string sourceWorkspace,
        string sourceWorkspaceRelativePath,
        string? branchName,
        string? requestedCommitSha,
        bool refreshRemote,
        Guid qaRunId,
        CancellationToken cancellationToken)
    {
        BranchQaGitSupport.NeedWorkspace(sourceWorkspace);

        if (string.IsNullOrWhiteSpace(branchName))
        {
            return new BranchQaWorkspaceLease(
                sourceWorkspace,
                sourceWorkspaceRelativePath,
                null,
                requestedCommitSha,
                "workspace",
                "NotRequested",
                null,
                null);
        }

        var branch = BranchQaGitSupport.NormalizeBranch(branchName);

        if (!await BranchQaGitSupport.IsGitRepositoryAsync(
                sourceWorkspace,
                cancellationToken))
        {
            return await PrepareArchiveBranchAsync(
                sourceWorkspace,
                sourceWorkspaceRelativePath,
                branch,
                requestedCommitSha,
                qaRunId,
                cancellationToken);
        }

        await BranchQaGitSupport.ValidateBranchAsync(
            sourceWorkspace,
            branch,
            cancellationToken);

        var (refreshStatus, refreshWarning) =
            await BranchQaGitSupport.RefreshAsync(
                sourceWorkspace,
                branch,
                refreshRemote,
                cancellationToken);

        var resolved = await BranchQaGitSupport.ResolveBranchRefAsync(
                           sourceWorkspace,
                           branch,
                           cancellationToken)
                       ?? throw new KeyNotFoundException(
                           $"Git branch '{branch}' is not available in the synchronized "
                           + "repository. Refresh the repository or fetch the branch first.");

        var commit = resolved.Sha;

        if (!string.IsNullOrWhiteSpace(requestedCommitSha))
        {
            var requested =
                BranchQaGitSupport.NormalizeCommit(requestedCommitSha);

            var verify = await BranchQaGitSupport.RunGitAsync(
                sourceWorkspace,
                ["merge-base", "--is-ancestor", requested, resolved.Ref],
                TimeSpan.FromSeconds(20),
                cancellationToken,
                throwOnStartFailure: false);

            if (verify.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Requested revision '{requested}' is not reachable from "
                    + $"branch '{branch}'. DIP will not label an unrelated commit "
                    + "as branch QA evidence.");
            }

            commit = requested;
        }

        var safeRelative =
            BranchQaGitSupport.NormalizeRelativeWorkspace(
                sourceWorkspaceRelativePath);

        var leaf = $"run-{qaRunId:N}";
        var targetRelative = Path.Combine(
                safeRelative,
                ".dip-branch-qa",
                leaf)
            .Replace('\\', '/');

        var targetPath = Path.Combine(
            sourceWorkspace,
            ".dip-branch-qa",
            leaf);

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        if (Directory.Exists(targetPath))
            Directory.Delete(targetPath, true);

        var add = await BranchQaGitSupport.RunGitAsync(
            sourceWorkspace,
            ["worktree", "add", "--detach", targetPath, commit],
            TimeSpan.FromMinutes(2),
            cancellationToken);

        BranchQaGitSupport.EnsureSuccess(
            add,
            $"Unable to create an isolated QA worktree for branch '{branch}'.");

        async ValueTask Cleanup()
        {
            try
            {
                _ = await BranchQaGitSupport.RunGitAsync(
                    sourceWorkspace,
                    ["worktree", "remove", "--force", targetPath],
                    TimeSpan.FromMinutes(1),
                    CancellationToken.None,
                    throwOnStartFailure: false);

                _ = await BranchQaGitSupport.RunGitAsync(
                    sourceWorkspace,
                    ["worktree", "prune"],
                    TimeSpan.FromSeconds(30),
                    CancellationToken.None,
                    throwOnStartFailure: false);
            }
            catch
            {
                // Git cleanup is best-effort; filesystem cleanup still runs.
            }

            try
            {
                if (Directory.Exists(targetPath))
                    Directory.Delete(targetPath, true);
            }
            catch
            {
                // A locked isolated directory can be removed by a later cleanup pass.
            }

            try
            {
                var parent = Path.GetDirectoryName(targetPath);
                if (parent is not null
                    && Directory.Exists(parent)
                    && !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    Directory.Delete(parent);
                }
            }
            catch
            {
                // Parent cleanup is cosmetic and must not fail a completed QA run.
            }
        }

        return new BranchQaWorkspaceLease(
            targetPath,
            targetRelative,
            branch,
            commit,
            resolved.Ref,
            refreshStatus,
            refreshWarning,
            Cleanup);
    }

    private async Task<BranchQaWorkspaceLease> PrepareArchiveBranchAsync(
        string sourceWorkspace,
        string sourceWorkspaceRelativePath,
        string branch,
        string? requestedCommitSha,
        Guid qaRunId,
        CancellationToken cancellationToken)
    {
        var marker = await BranchQaGitSupport.ReadSyncMarkerAsync(
                         sourceWorkspace,
                         cancellationToken)
                     ?? throw new InvalidOperationException(
                         "This workspace has no Git metadata and no GitHub "
                         + "synchronization provenance. Branch QA cannot establish "
                         + "branch identity safely.");

        if (_github is null)
        {
            throw new InvalidOperationException(
                "GitHub repository access is required to verify a branch "
                + "from an archive-backed workspace.");
        }

        BranchQaGitSupport.ValidatePortableBranch(branch);
        var (owner, repository) =
            BranchQaGitSupport.ParseRepository(marker.Repository);

        var snapshot = await _github.SyncWorkspaceAsync(
            qaRunId,
            owner,
            repository,
            branch,
            cancellationToken);

        try
        {
            if (!snapshot.Branch.Equals(
                    branch,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "GitHub branch snapshot returned unexpected branch provenance.");
            }

            if (!string.IsNullOrWhiteSpace(requestedCommitSha))
            {
                var requested =
                    BranchQaGitSupport.NormalizeCommit(requestedCommitSha);

                if (!snapshot.CommitSha.Equals(
                        requested,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Requested revision '{requested}' is not the current head "
                        + $"of branch '{branch}'. Archive-backed QA refuses to "
                        + "relabel another revision as branch evidence.");
                }
            }

            var workspaceRoot =
                BranchQaGitSupport.ResolveWorkspaceRoot(
                    sourceWorkspace,
                    marker.WorkspaceRelativePath);

            var targetPath = Path.GetFullPath(
                Path.Combine(
                    workspaceRoot,
                    snapshot.WorkspaceRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));

            BranchQaGitSupport.EnsureInside(workspaceRoot, targetPath);

            if (targetPath.Equals(
                    Path.GetFullPath(sourceWorkspace),
                    BranchQaGitSupport.PathComparison))
            {
                throw new InvalidOperationException(
                    "Branch QA snapshot resolved to the shared workspace; "
                    + "isolation was refused.");
            }

            if (!Directory.Exists(targetPath))
            {
                throw new DirectoryNotFoundException(
                    "GitHub branch snapshot was not created.");
            }

            async ValueTask Cleanup() =>
                await BranchQaGitSupport.DeleteDirectoryAsync(targetPath);

            return new BranchQaWorkspaceLease(
                targetPath,
                snapshot.WorkspaceRelativePath,
                branch,
                snapshot.CommitSha,
                $"github:{owner}/{repository}:refs/heads/{branch}",
                "GitHubArchive",
                null,
                Cleanup);
        }
        catch
        {
            await BranchQaGitSupport.DeleteArchiveSnapshotAsync(
                sourceWorkspace,
                marker,
                snapshot);
            throw;
        }
    }
}
