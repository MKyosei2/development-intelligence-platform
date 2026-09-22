// Purpose: Provides the stable IGitHubRepositoryCatalog facade while authentication, repositories, pull requests, JSON mapping, and workspace materialization are separated.
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHubIntegration;
using Dip.Infrastructure.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.GitHub;

public sealed class GitHubRepositoryCatalog : IGitHubRepositoryCatalog, IDisposable
{
    private readonly GitHubAuthenticationProvider _auth;
    private readonly GitHubRepositoryClient _repositories;
    private readonly GitHubPullRequestClient _pullRequests;
    private readonly GitHubWorkspaceMaterializer _workspace;

    public GitHubRepositoryCatalog(IOptions<GitHubOptions> options, IOptions<QaExecutionOptions> qaOptions)
    {
        _auth = new(options.Value);
        _repositories = new(_auth);
        _pullRequests = new(_auth);
        _workspace = new(_auth, _repositories, qaOptions.Value.WorkspaceRoot);
    }

    public Task<GitHubConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken) =>
        _auth.GetStatusAsync(cancellationToken);

    public Task<IReadOnlyList<GitHubRepositorySummaryDto>> ListRepositoriesAsync(string? search, string? visibility, CancellationToken cancellationToken) =>
        _repositories.ListRepositoriesAsync(search, visibility, cancellationToken);

    public Task<GitHubRepositoryDetailsDto> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken) =>
        _repositories.GetRepositoryAsync(owner, repository, cancellationToken);

    public Task<IReadOnlyList<GitHubPullRequestSnapshotDto>> ListPullRequestsAsync(string owner, string repository, string state, CancellationToken cancellationToken) =>
        _pullRequests.ListPullRequestsAsync(owner, repository, state, cancellationToken);

    public Task<GitHubWorkspaceSyncDto> SyncWorkspaceAsync(Guid projectId, string owner, string repository, string branch, CancellationToken cancellationToken) =>
        _workspace.SyncWorkspaceAsync(projectId, owner, repository, branch, cancellationToken);

    public Task<GitHubWorkspaceSyncDto?> GetWorkspaceAsync(Guid projectId, CancellationToken cancellationToken) =>
        _workspace.GetWorkspaceAsync(projectId, cancellationToken);

    public void Dispose() => _auth.Dispose();
}
