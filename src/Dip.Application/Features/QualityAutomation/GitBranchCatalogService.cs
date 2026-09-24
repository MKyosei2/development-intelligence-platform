// Purpose: Discovers local or GitHub-backed branch references and computes reviewer-visible branch comparison metadata.
using System.Globalization;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHubIntegration;

namespace Dip.Application.Features.QualityAutomation;

internal sealed class GitBranchCatalogService
{
    private const int MaxBranches = 250;
    private readonly IGitHubRepositoryCatalog? _github;

    internal GitBranchCatalogService(IGitHubRepositoryCatalog? github) =>
        _github = github;

    internal async Task<BranchRepositoryCatalogDto> ListAsync(
        string sourceWorkspace,
        string defaultBranch,
        bool refreshRemote,
        CancellationToken cancellationToken)
    {
        BranchQaGitSupport.NeedWorkspace(sourceWorkspace);
        var normalizedDefault = BranchQaGitSupport.NormalizeBranch(defaultBranch);

        if (!await BranchQaGitSupport.IsGitRepositoryAsync(
                sourceWorkspace,
                cancellationToken))
        {
            return await ListArchiveBranchesAsync(
                sourceWorkspace,
                normalizedDefault,
                cancellationToken);
        }

        var (refreshStatus, refreshWarning) =
            await BranchQaGitSupport.RefreshAsync(
                sourceWorkspace,
                null,
                refreshRemote,
                cancellationToken);

        var refs = await BranchQaGitSupport.RunGitAsync(
            sourceWorkspace,
            [
                "for-each-ref",
                "--format=%(refname)|%(objectname)|%(committerdate:iso-strict)",
                "refs/heads",
                "refs/remotes/origin"
            ],
            TimeSpan.FromSeconds(30),
            cancellationToken);

        BranchQaGitSupport.EnsureSuccess(
            refs,
            "Unable to enumerate Git branches.");

        var selected =
            new Dictionary<
                string,
                (string Ref, string Sha, bool Remote, DateTimeOffset? At)>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var line in BranchQaGitSupport.Lines(refs.StandardOutput))
        {
            var parts = line.Split('|', 3);
            if (parts.Length < 2)
                continue;

            var fullRef = parts[0].Trim();
            if (fullRef.Equals(
                    "refs/remotes/origin/HEAD",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remote = fullRef.StartsWith(
                "refs/remotes/origin/",
                StringComparison.Ordinal);
            var local = fullRef.StartsWith(
                "refs/heads/",
                StringComparison.Ordinal);

            if (!remote && !local)
                continue;

            var branch = remote
                ? fullRef["refs/remotes/origin/".Length..]
                : fullRef["refs/heads/".Length..];

            if (string.IsNullOrWhiteSpace(branch))
                continue;

            DateTimeOffset? committedAt = null;
            if (parts.Length == 3
                && DateTimeOffset.TryParse(
                    parts[2],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsed))
            {
                committedAt = parsed;
            }

            if (!selected.TryGetValue(branch, out var existing)
                || remote && !existing.Remote)
            {
                selected[branch] = (
                    fullRef,
                    parts[1].Trim(),
                    remote,
                    committedAt);
            }
        }

        var defaultRef = await BranchQaGitSupport.ResolveBranchRefAsync(
            sourceWorkspace,
            normalizedDefault,
            cancellationToken);

        var output = new List<BranchRepositoryRefDto>();

        foreach (var item in selected
                     .OrderBy(
                         x => x.Key.Equals(
                             normalizedDefault,
                             StringComparison.OrdinalIgnoreCase)
                             ? 0
                             : 1)
                     .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(MaxBranches))
        {
            var ahead = 0;
            var behind = 0;
            string? warning = null;

            if (defaultRef is not null)
            {
                var delta = await BranchQaGitSupport.RunGitAsync(
                    sourceWorkspace,
                    [
                        "rev-list",
                        "--left-right",
                        "--count",
                        $"{defaultRef.Value.Ref}...{item.Value.Ref}"
                    ],
                    TimeSpan.FromSeconds(20),
                    cancellationToken,
                    throwOnStartFailure: false);

                if (delta.ExitCode == 0)
                {
                    var bits = delta.StandardOutput.Trim().Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries);

                    if (bits.Length >= 2)
                    {
                        int.TryParse(bits[0], out behind);
                        int.TryParse(bits[1], out ahead);
                    }
                }
                else
                {
                    warning = "Ahead/behind comparison was unavailable.";
                }
            }

            output.Add(
                new BranchRepositoryRefDto(
                    item.Key,
                    item.Value.Sha,
                    item.Key.Equals(
                        normalizedDefault,
                        StringComparison.OrdinalIgnoreCase),
                    item.Value.Remote,
                    ahead,
                    behind,
                    item.Value.At,
                    item.Value.Remote ? "origin" : "local",
                    refreshStatus,
                    warning));
        }

        if (output.All(
                x => !x.Branch.Equals(
                    normalizedDefault,
                    StringComparison.OrdinalIgnoreCase))
            && defaultRef is not null)
        {
            output.Insert(
                0,
                new BranchRepositoryRefDto(
                    normalizedDefault,
                    defaultRef.Value.Sha,
                    true,
                    defaultRef.Value.Remote,
                    0,
                    0,
                    null,
                    defaultRef.Value.Remote ? "origin" : "local",
                    refreshStatus,
                    null));
        }

        return new BranchRepositoryCatalogDto(
            normalizedDefault,
            refreshStatus,
            output,
            refreshWarning,
            DateTimeOffset.UtcNow);
    }

    private async Task<BranchRepositoryCatalogDto> ListArchiveBranchesAsync(
        string sourceWorkspace,
        string defaultBranch,
        CancellationToken cancellationToken)
    {
        var marker = await BranchQaGitSupport.ReadSyncMarkerAsync(
                         sourceWorkspace,
                         cancellationToken)
                     ?? throw new InvalidOperationException(
                         "Branch QA requires either a Git working tree or a "
                         + "GitHub-synchronized workspace with .dip-sync.json provenance.");

        if (_github is null)
        {
            throw new InvalidOperationException(
                "GitHub branch catalog is unavailable. Configure GitHub repository "
                + "access or use a Git working tree workspace.");
        }

        var (owner, repository) =
            BranchQaGitSupport.ParseRepository(marker.Repository);

        var details = await _github.GetRepositoryAsync(
            owner,
            repository,
            cancellationToken);

        IReadOnlyList<GitHubPullRequestSnapshotDto> pulls;
        try
        {
            pulls = await _github.ListPullRequestsAsync(
                owner,
                repository,
                "open",
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            pulls = Array.Empty<GitHubPullRequestSnapshotDto>();
        }

        var refs =
            new Dictionary<
                string,
                (string Sha, DateTimeOffset? At, string Source)>(
                StringComparer.OrdinalIgnoreCase);

        void Observe(
            string? branch,
            string? sha,
            DateTimeOffset? at,
            string source)
        {
            if (string.IsNullOrWhiteSpace(branch))
                return;

            var normalized = branch.Trim();
            if (normalized.Length > 200)
                return;

            var revision = sha?.Trim() ?? string.Empty;
            if (!refs.TryGetValue(normalized, out var existing)
                || at.HasValue
                && (!existing.At.HasValue || at > existing.At))
            {
                refs[normalized] = (revision, at, source);
            }
        }

        Observe(
            defaultBranch,
            details.LatestCommitSha,
            details.LatestCommitAtUtc,
            "github-default");

        foreach (var run in details.WorkflowRuns)
        {
            Observe(
                run.HeadBranch,
                run.HeadSha,
                run.UpdatedAtUtc ?? run.CreatedAtUtc,
                "github-actions");
        }

        foreach (var pull in pulls)
        {
            Observe(
                pull.HeadBranch,
                pull.HeadSha,
                null,
                $"pull-request:{pull.Number}");
        }

        Observe(
            marker.Branch,
            marker.CommitSha,
            marker.SyncedAtUtc,
            "workspace-snapshot");

        var branches = refs
            .OrderBy(
                x => x.Key.Equals(
                    defaultBranch,
                    StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxBranches)
            .Select(
                x => new BranchRepositoryRefDto(
                    x.Key,
                    x.Value.Sha,
                    x.Key.Equals(
                        defaultBranch,
                        StringComparison.OrdinalIgnoreCase),
                    true,
                    0,
                    0,
                    x.Value.At,
                    x.Value.Source,
                    "GitHubApi",
                    "Ahead/behind requires a Git working tree; branch QA itself "
                    + "uses an isolated authenticated GitHub archive snapshot."))
            .ToArray();

        return new BranchRepositoryCatalogDto(
            defaultBranch,
            "GitHubApi",
            branches,
            "Archive-backed workspace: active branches are discovered from the "
            + "default branch, open pull requests and the synchronized snapshot; "
            + "persisted DIP pipeline history is merged by the branch-quality service. "
            + "Any other valid branch can still be entered and verified explicitly.",
            DateTimeOffset.UtcNow);
    }
}
