// Purpose: Coordinates branch catalog discovery and isolated QA workspace preparation without owning Git process or cleanup details.
using Dip.Application.Abstractions.GitHub;

namespace Dip.Application.Features.QualityAutomation;

public sealed record BranchRepositoryRefDto(
    string Branch,
    string CommitSha,
    bool IsDefault,
    bool RemoteTracked,
    int AheadOfDefault,
    int BehindDefault,
    DateTimeOffset? LastCommitAtUtc,
    string Source,
    string RefreshStatus,
    string? Warning);

public sealed record BranchRepositoryCatalogDto(
    string DefaultBranch,
    string RefreshStatus,
    IReadOnlyList<BranchRepositoryRefDto> Branches,
    string? Warning,
    DateTimeOffset GeneratedAtUtc);

public interface IBranchQaWorkspaceManager
{
    Task<BranchRepositoryCatalogDto> ListBranchesAsync(
        string sourceWorkspace,
        string defaultBranch,
        bool refreshRemote,
        CancellationToken cancellationToken);

    Task<BranchQaWorkspaceLease> PrepareAsync(
        string sourceWorkspace,
        string sourceWorkspaceRelativePath,
        string? branchName,
        string? requestedCommitSha,
        bool refreshRemote,
        Guid qaRunId,
        CancellationToken cancellationToken);
}

public sealed class GitBranchQaWorkspaceManager : IBranchQaWorkspaceManager
{
    private readonly GitBranchCatalogService _catalog;
    private readonly GitBranchWorkspaceFactory _workspaces;

    public GitBranchQaWorkspaceManager(IGitHubRepositoryCatalog? github = null)
    {
        _catalog = new GitBranchCatalogService(github);
        _workspaces = new GitBranchWorkspaceFactory(github);
    }

    public Task<BranchRepositoryCatalogDto> ListBranchesAsync(
        string sourceWorkspace,
        string defaultBranch,
        bool refreshRemote,
        CancellationToken cancellationToken) =>
        _catalog.ListAsync(
            sourceWorkspace,
            defaultBranch,
            refreshRemote,
            cancellationToken);

    public Task<BranchQaWorkspaceLease> PrepareAsync(
        string sourceWorkspace,
        string sourceWorkspaceRelativePath,
        string? branchName,
        string? requestedCommitSha,
        bool refreshRemote,
        Guid qaRunId,
        CancellationToken cancellationToken) =>
        _workspaces.PrepareAsync(
            sourceWorkspace,
            sourceWorkspaceRelativePath,
            branchName,
            requestedCommitSha,
            refreshRemote,
            qaRunId,
            cancellationToken);
}
