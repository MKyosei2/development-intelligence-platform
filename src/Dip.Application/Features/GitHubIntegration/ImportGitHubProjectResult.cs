// Purpose: Defines the ImportGitHubProjectResult application contract.
namespace Dip.Application.Features.GitHubIntegration;

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
