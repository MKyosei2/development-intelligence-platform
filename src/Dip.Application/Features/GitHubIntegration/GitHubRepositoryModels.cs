// Purpose: Coordinates G it Hu bR ep os it or yM od el s application behavior for GitHubIntegration.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubConnectionStatusDto(
    bool Configured,
    bool Connected,
    string Mode,
    long? InstallationId,
    string? Account,
    string? AppSlug,
    string? InstallUrl,
    string Message);

public sealed record GitHubRepositorySummaryDto(
    long Id,
    string Owner,
    string Name,
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    bool Private,
    bool Archived,
    string? PrimaryLanguage,
    string? Description,
    int OpenIssuesCount,
    int StarsCount,
    int ForksCount,
    DateTimeOffset? UpdatedAtUtc,
    Guid? DipProjectId = null);

public sealed record GitHubWorkflowRunDto(
    long Id,
    string Name,
    string Event,
    string Status,
    string? Conclusion,
    string HeadBranch,
    string HeadSha,
    string HtmlUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record GitHubPullRequestFileDto(
    string Path,
    string Status,
    int Additions,
    int Deletions);

public sealed record GitHubPullRequestReviewDto(
    string Reviewer,
    string State,
    DateTimeOffset? SubmittedAtUtc);

public sealed record GitHubPullRequestSnapshotDto(
    int Number,
    string Title,
    string State,
    string HeadSha,
    string HeadBranch,
    string HtmlUrl,
    bool Merged,
    IReadOnlyList<string> CommitShas,
    IReadOnlyList<GitHubPullRequestFileDto> Files,
    IReadOnlyList<GitHubPullRequestReviewDto> Reviews);

public sealed record GitHubCapabilityProbeDto(
    bool Metadata,
    bool Contents,
    bool PullRequests,
    bool Actions);

public sealed record GitHubRepositoryDetailsDto(
    GitHubRepositorySummaryDto Repository,
    string? LatestCommitSha,
    string? LatestCommitMessage,
    DateTimeOffset? LatestCommitAtUtc,
    IReadOnlyList<GitHubWorkflowRunDto> WorkflowRuns,
    IReadOnlyList<GitHubPullRequestSnapshotDto>? PullRequests = null,
    GitHubCapabilityProbeDto? Capabilities = null);

public sealed record GitHubWorkspaceSyncDto(
    Guid ProjectId,
    string Repository,
    string Branch,
    string WorkspaceRelativePath,
    string CommitSha,
    int FileCount,
    long ExtractedBytes,
    DateTimeOffset SyncedAtUtc);

public sealed record GitHubProjectRepositoryDto(
    Guid ProjectId,
    string ProjectName,
    string RepositoryUrl,
    string DefaultBranch,
    GitHubConnectionStatusDto Connection,
    GitHubRepositoryDetailsDto? Repository,
    GitHubWorkspaceSyncDto? Workspace,
    string? Warning);

public sealed record ImportGitHubProjectRequest(
    string Repository,
    string? ProjectName = null,
    DateTimeOffset? ReleaseDateUtc = null,
    bool SyncWorkspace = true,
    bool RunInitialQa = true);

public sealed record ImportGitHubProjectResult(
    Guid ProjectId,
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    bool WorkspaceSynced,
    GitHubWorkspaceSyncDto? Workspace,
    string? Warning = null,
    Guid? InitialQaRunId = null,
    string? InitialQaStatus = null,
    int InitialQaFindingCount = 0,
    int InitialQaCriticalCount = 0);
