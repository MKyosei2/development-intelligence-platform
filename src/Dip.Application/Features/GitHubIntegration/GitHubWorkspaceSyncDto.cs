// Purpose: Defines the GitHubWorkspaceSyncDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubWorkspaceSyncDto(
    Guid ProjectId,
    string Repository,
    string Branch,
    string WorkspaceRelativePath,
    string CommitSha,
    int FileCount,
    long ExtractedBytes,
    DateTimeOffset SyncedAtUtc);
