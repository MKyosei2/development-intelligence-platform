// Purpose: Provides the infrastructure capability represented by G it Hu bR ep os it or yC at al og.
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
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly string _workspaceRoot;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _installationToken;
    private DateTimeOffset _installationTokenExpiresAtUtc;

    public GitHubRepositoryCatalog(
        IOptions<GitHubOptions> options,
        IOptions<QaExecutionOptions> qaOptions)
    {
        _options = options.Value;
        _workspaceRoot = Path.GetFullPath(qaOptions.Value.WorkspaceRoot);
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            BaseAddress = new Uri(EnsureTrailingSlash(
                string.IsNullOrWhiteSpace(_options.ApiBaseUrl)
                    ? "https://api.github.com/"
                    : _options.ApiBaseUrl)),
            Timeout = TimeSpan.FromMinutes(10)
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            string.IsNullOrWhiteSpace(_options.ProductHeader)
                ? "development-intelligence-platform"
                : _options.ProductHeader);
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<GitHubConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var mode = ResolveMode();
        if (mode == "NotConfigured")
        {
            return new GitHubConnectionStatusDto(
                false,
                false,
                mode,
                null,
                null,
                EmptyToNull(_options.AppSlug),
                BuildInstallUrl(),
                "Configure a GitHub App installation or a read-only GitHub token to browse repositories.");
        }

        try
        {
            if (mode == "GitHubApp")
            {
                using var request = await CreateRequestAsync(
                    HttpMethod.Get,
                    "installation/repositories?per_page=1",
                    cancellationToken);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = await ReadJsonAsync(response, cancellationToken);
                var account = TryFirstOwner(document.RootElement);
                return new GitHubConnectionStatusDto(
                    true,
                    true,
                    mode,
                    _options.InstallationId,
                    account,
                    EmptyToNull(_options.AppSlug),
                    BuildInstallUrl(),
                    "GitHub App installation is connected. Repository access follows the installation selection.");
            }

            using var userRequest = await CreateRequestAsync(
                HttpMethod.Get,
                "user",
                cancellationToken);
            using var userResponse = await _httpClient.SendAsync(userRequest, cancellationToken);
            userResponse.EnsureSuccessStatusCode();
            using var userDocument = await ReadJsonAsync(userResponse, cancellationToken);
            var login = GetString(userDocument.RootElement, "login");
            return new GitHubConnectionStatusDto(
                true,
                true,
                mode,
                null,
                login,
                EmptyToNull(_options.AppSlug),
                BuildInstallUrl(),
                "GitHub token connection is active. Prefer GitHub App mode for portfolio and production deployments.");
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or CryptographicException)
        {
            return new GitHubConnectionStatusDto(
                true,
                false,
                mode,
                _options.InstallationId > 0 ? _options.InstallationId : null,
                null,
                EmptyToNull(_options.AppSlug),
                BuildInstallUrl(),
                "GitHub connection failed: " + SafeMessage(exception));
        }
    }

    public async Task<IReadOnlyList<GitHubRepositorySummaryDto>> ListRepositoriesAsync(
        string? search,
        string? visibility,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var results = new List<GitHubRepositorySummaryDto>();
        var maxPages = Math.Clamp(_options.MaxRepositoryPages, 1, 50);

        for (var page = 1; page <= maxPages; page++)
        {
            var path = _options.HasGitHubAppCredentials
                ? $"installation/repositories?per_page=100&page={page}"
                : $"user/repos?per_page=100&page={page}&sort=updated&affiliation=owner,collaborator,organization_member";

            using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, cancellationToken);
            var array = _options.HasGitHubAppCredentials
                ? document.RootElement.GetProperty("repositories")
                : document.RootElement;

            var count = 0;
            foreach (var item in array.EnumerateArray())
            {
                count++;
                var mapped = MapRepository(item);
                if (!MatchesSearch(mapped, search) || !MatchesVisibility(mapped, visibility))
                    continue;
                results.Add(mapped);
            }

            if (count < 100)
                break;
        }

        return results
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GitHubRepositoryDetailsDto> GetRepositoryAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        ValidateOwnerRepository(owner, repository);
        EnsureConfigured();

        var repoPath = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}";
        using var repoRequest = await CreateRequestAsync(HttpMethod.Get, repoPath, cancellationToken);
        using var repoResponse = await _httpClient.SendAsync(repoRequest, cancellationToken);
        repoResponse.EnsureSuccessStatusCode();
        using var repoDocument = await ReadJsonAsync(repoResponse, cancellationToken);
        var summary = MapRepository(repoDocument.RootElement);

        var contentsReadable = false;
        string? latestSha = null;
        string? latestMessage = null;
        DateTimeOffset? latestAt = null;
        try
        {
            using var commitRequest = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/commits/{Uri.EscapeDataString(summary.DefaultBranch)}",
                cancellationToken);
            using var commitResponse = await _httpClient.SendAsync(commitRequest, cancellationToken);
            contentsReadable = commitResponse.IsSuccessStatusCode;
            if (contentsReadable)
            {
                using var commitDocument = await ReadJsonAsync(commitResponse, cancellationToken);
                latestSha = GetString(commitDocument.RootElement, "sha");
                if (commitDocument.RootElement.TryGetProperty("commit", out var commit))
                {
                    latestMessage = GetString(commit, "message")?.Split('\n')[0];
                    if (commit.TryGetProperty("committer", out var committer))
                        latestAt = GetDate(committer, "date");
                }
            }
        }
        catch (HttpRequestException)
        {
            contentsReadable = false;
        }

        var actionsReadable = false;
        var runs = new List<GitHubWorkflowRunDto>();
        try
        {
            using var runsRequest = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/actions/runs?branch={Uri.EscapeDataString(summary.DefaultBranch)}&per_page=30",
                cancellationToken);
            using var runsResponse = await _httpClient.SendAsync(runsRequest, cancellationToken);
            actionsReadable = runsResponse.IsSuccessStatusCode;
            if (actionsReadable)
            {
                using var runsDocument = await ReadJsonAsync(runsResponse, cancellationToken);
                if (runsDocument.RootElement.TryGetProperty("workflow_runs", out var array))
                {
                    foreach (var run in array.EnumerateArray())
                        runs.Add(MapWorkflowRun(run));
                }
            }
        }
        catch (HttpRequestException)
        {
            actionsReadable = false;
        }

        var pullRequestsReadable = false;
        try
        {
            using var pullsRequest = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls?state=open&sort=updated&direction=desc&per_page=1&page=1",
                cancellationToken);
            using var pullsResponse = await _httpClient.SendAsync(pullsRequest, cancellationToken);
            pullRequestsReadable = pullsResponse.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            pullRequestsReadable = false;
        }

        var pullRequests = Array.Empty<GitHubPullRequestSnapshotDto>();

        return new GitHubRepositoryDetailsDto(
            summary,
            latestSha,
            latestMessage,
            latestAt,
            runs,
            pullRequests,
            new GitHubCapabilityProbeDto(
                Metadata: true,
                Contents: contentsReadable,
                PullRequests: pullRequestsReadable,
                Actions: actionsReadable));
    }

    public async Task<IReadOnlyList<GitHubPullRequestSnapshotDto>> ListPullRequestsAsync(
        string owner,
        string repository,
        string state,
        CancellationToken cancellationToken)
    {
        ValidateOwnerRepository(owner, repository);
        EnsureConfigured();

        var normalizedState = (state ?? "all").Trim().ToLowerInvariant() switch
        {
            "open" => "open",
            "closed" => "closed",
            "all" => "all",
            _ => throw new ArgumentException("Pull request state must be open, closed, or all.", nameof(state))
        };
        var repoPath = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}";
        var results = new List<GitHubPullRequestSnapshotDto>();
        var maxPages = Math.Clamp(_options.MaxPullRequestPages, 1, 50);

        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls?state={normalizedState}&sort=updated&direction=desc&per_page=100&page={page}",
                cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                count++;
                var number = GetInt32(item, "number");
                if (number <= 0)
                    continue;
                var head = item.TryGetProperty("head", out var headNode) ? headNode : default;
                var commits = await LoadPullRequestCommitsAsync(repoPath, number, cancellationToken);
                var files = await LoadPullRequestFilesAsync(repoPath, number, cancellationToken);
                var reviews = await LoadPullRequestReviewsAsync(repoPath, number, cancellationToken);
                results.Add(new GitHubPullRequestSnapshotDto(
                    number,
                    GetString(item, "title") ?? $"Pull request #{number}",
                    GetString(item, "state") ?? "open",
                    head.ValueKind == JsonValueKind.Object ? GetString(head, "sha") ?? string.Empty : string.Empty,
                    head.ValueKind == JsonValueKind.Object ? GetString(head, "ref") ?? string.Empty : string.Empty,
                    GetString(item, "html_url") ?? string.Empty,
                    GetDate(item, "merged_at").HasValue,
                    commits.Count > 0
                        ? commits
                        : new[] { head.ValueKind == JsonValueKind.Object ? GetString(head, "sha") ?? string.Empty : string.Empty }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                    files,
                    reviews));
            }

            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull-request backfill reached the configured {maxPages * 100} item safety limit. Increase GitHub:MaxPullRequestPages to continue the explicit backfill.");
        }

        return results
            .OrderByDescending(item => item.Number)
            .ToArray();
    }

    public async Task<GitHubWorkspaceSyncDto> SyncWorkspaceAsync(
        Guid projectId,
        string owner,
        string repository,
        string branch,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        ValidateOwnerRepository(owner, repository);
        if (string.IsNullOrWhiteSpace(branch))
            throw new ArgumentException("Branch is required.", nameof(branch));
        EnsureConfigured();

        Directory.CreateDirectory(_workspaceRoot);
        var relative = Path.Combine("github", projectId.ToString("N"));
        var target = Path.GetFullPath(Path.Combine(_workspaceRoot, relative));
        EnsureInsideWorkspace(target);

        var syncRoot = Path.Combine(_workspaceRoot, "github", ".sync");
        Directory.CreateDirectory(syncRoot);
        var nonce = Guid.NewGuid().ToString("N");
        var archive = Path.Combine(syncRoot, $"{projectId:N}-{nonce}.zip");
        var extractRoot = Path.Combine(syncRoot, $"{projectId:N}-{nonce}");
        Directory.CreateDirectory(extractRoot);

        try
        {
            var repoPath = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}";
            using var request = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/zipball/{Uri.EscapeDataString(branch)}",
                cancellationToken);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var maxArchiveBytes = (long)Math.Clamp(_options.WorkspaceMaxArchiveMb, 32, 2048) * 1024L * 1024L;
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > maxArchiveBytes)
            {
                throw new InvalidOperationException(
                    $"GitHub archive is larger than the configured {maxArchiveBytes / 1024 / 1024} MB limit.");
            }

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(
                archive,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 128];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                        break;
                    total += read;
                    if (total > maxArchiveBytes)
                        throw new InvalidOperationException("GitHub archive exceeded the configured size limit while downloading.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            var extracted = ExtractRepositoryArchive(archive, extractRoot, maxArchiveBytes * 4);
            // Resolve the exact revision of the branch that was requested. Using the
            // repository default-branch SHA here makes workspace freshness incorrect
            // for projects whose DIP default branch differs from GitHub's default.
            var branchCommitSha = await GetBranchCommitShaAsync(repoPath, branch, cancellationToken);
            var details = await GetRepositoryAsync(owner, repository, cancellationToken);
            var commitSha = branchCommitSha ?? details.LatestCommitSha ?? branch;
            var sync = new GitHubWorkspaceSyncDto(
                projectId,
                $"{owner}/{repository}",
                branch,
                relative.Replace('\\', '/'),
                commitSha,
                extracted.FileCount,
                extracted.ExtractedBytes,
                DateTimeOffset.UtcNow);

            await File.WriteAllTextAsync(
                Path.Combine(extractRoot, ".dip-sync.json"),
                JsonSerializer.Serialize(sync, JsonOptions),
                cancellationToken);

            ReplaceDirectory(extractRoot, target);
            return sync;
        }
        finally
        {
            TryDeleteFile(archive);
            TryDeleteDirectory(extractRoot);
        }
    }

    public async Task<GitHubWorkspaceSyncDto?> GetWorkspaceAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            return null;
        var target = Path.GetFullPath(Path.Combine(_workspaceRoot, "github", projectId.ToString("N")));
        EnsureInsideWorkspace(target);
        var marker = Path.Combine(target, ".dip-sync.json");
        if (!File.Exists(marker))
            return null;
        try
        {
            var json = await File.ReadAllTextAsync(marker, cancellationToken);
            return JsonSerializer.Deserialize<GitHubWorkspaceSyncDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> GetBranchCommitShaAsync(
        string repoPath,
        string branch,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"{repoPath}/commits/{Uri.EscapeDataString(branch)}",
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        using var document = await ReadJsonAsync(response, cancellationToken);
        return GetString(document.RootElement, "sha");
    }

    private async Task<IReadOnlyList<string>> LoadPullRequestCommitsAsync(
        string repoPath,
        int number,
        CancellationToken cancellationToken)
    {
        var results = new List<string>();
        var maxPages = Math.Clamp(_options.MaxPullRequestCommitPages, 1, 3);
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls/{number}/commits?per_page=100&page={page}",
                cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                count++;
                var sha = GetString(item, "sha");
                if (!string.IsNullOrWhiteSpace(sha))
                    results.Add(sha);
            }
            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull request #{number} commit history reached the configured pagination safety limit.");
        }
        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<IReadOnlyList<GitHubPullRequestFileDto>> LoadPullRequestFilesAsync(
        string repoPath,
        int number,
        CancellationToken cancellationToken)
    {
        var results = new List<GitHubPullRequestFileDto>();
        var maxPages = Math.Clamp(_options.MaxPullRequestFilePages, 1, 30);
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls/{number}/files?per_page=100&page={page}",
                cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                count++;
                var path = GetString(item, "filename") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                results.Add(new GitHubPullRequestFileDto(
                    path,
                    GetString(item, "status") ?? "modified",
                    GetInt32(item, "additions"),
                    GetInt32(item, "deletions")));
            }
            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull request #{number} changed-file history reached the configured pagination safety limit.");
        }
        return results;
    }

    private async Task<IReadOnlyList<GitHubPullRequestReviewDto>> LoadPullRequestReviewsAsync(
        string repoPath,
        int number,
        CancellationToken cancellationToken)
    {
        var results = new List<GitHubPullRequestReviewDto>();
        var maxPages = Math.Clamp(_options.MaxPullRequestReviewPages, 1, 50);
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls/{number}/reviews?per_page=100&page={page}",
                cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                count++;
                var reviewer = item.TryGetProperty("user", out var user)
                    ? GetString(user, "login") ?? "reviewer"
                    : "reviewer";
                results.Add(new GitHubPullRequestReviewDto(
                    reviewer,
                    GetString(item, "state") ?? "commented",
                    GetDate(item, "submitted_at")));
            }
            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull request #{number} review history reached the configured pagination safety limit.");
        }
        return results;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!_options.HasGitHubAppCredentials)
        {
            if (string.IsNullOrWhiteSpace(_options.Token))
                throw new InvalidOperationException("GitHub repository access is not configured.");
            return _options.Token.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_installationToken)
            && _installationTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _installationToken;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_installationToken)
                && _installationTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _installationToken;
            }

            var jwt = CreateAppJwt();
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"app/installations/{_options.InstallationId}/access_tokens");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, cancellationToken);
            _installationToken = GetString(document.RootElement, "token")
                ?? throw new InvalidOperationException("GitHub App token response did not contain token.");
            _installationTokenExpiresAtUtc = GetDate(document.RootElement, "expires_at")
                ?? DateTimeOffset.UtcNow.AddMinutes(50);
            return _installationToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private string CreateAppJwt()
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = _options.AppId
        });
        var payloadPart = Base64Url(payload);
        var signingInput = Encoding.ASCII.GetBytes(header + "." + payloadPart);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(LoadPrivateKeyPem());
        var signature = rsa.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return header + "." + payloadPart + "." + Base64Url(signature);
    }

    private string LoadPrivateKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(_options.AppPrivateKeyBase64))
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(_options.AppPrivateKeyBase64.Trim()));
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("GitHub:AppPrivateKeyBase64 is not valid Base64.", exception);
            }
        }

        if (string.IsNullOrWhiteSpace(_options.AppPrivateKeyPath))
            throw new InvalidOperationException("GitHub App private key is not configured.");
        var fullPath = Path.GetFullPath(_options.AppPrivateKeyPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("GitHub App private key file was not found.", fullPath);
        return File.ReadAllText(fullPath);
    }

    private void EnsureConfigured()
    {
        if (!_options.HasGitHubAppCredentials && string.IsNullOrWhiteSpace(_options.Token))
            throw new InvalidOperationException("GitHub repository access is not configured.");
    }

    private string ResolveMode()
    {
        if (_options.HasGitHubAppCredentials)
            return "GitHubApp";
        if (!string.IsNullOrWhiteSpace(_options.Token))
            return "Token";
        return "NotConfigured";
    }

    private string? BuildInstallUrl() => string.IsNullOrWhiteSpace(_options.AppSlug)
        ? null
        : $"https://github.com/apps/{Uri.EscapeDataString(_options.AppSlug.Trim())}/installations/new";

    private static GitHubRepositorySummaryDto MapRepository(JsonElement item)
    {
        var owner = item.TryGetProperty("owner", out var ownerNode)
            ? GetString(ownerNode, "login") ?? string.Empty
            : string.Empty;
        var name = GetString(item, "name") ?? string.Empty;
        return new GitHubRepositorySummaryDto(
            GetInt64(item, "id"),
            owner,
            name,
            GetString(item, "full_name") ?? $"{owner}/{name}",
            GetString(item, "html_url") ?? string.Empty,
            GetString(item, "default_branch") ?? "main",
            GetBoolean(item, "private"),
            GetBoolean(item, "archived"),
            GetString(item, "language"),
            GetString(item, "description"),
            GetInt32(item, "open_issues_count"),
            GetInt32(item, "stargazers_count"),
            GetInt32(item, "forks_count"),
            GetDate(item, "updated_at"));
    }

    private static GitHubWorkflowRunDto MapWorkflowRun(JsonElement run) => new(
        GetInt64(run, "id"),
        GetString(run, "name") ?? GetString(run, "display_title") ?? "Workflow",
        GetString(run, "event") ?? "unknown",
        GetString(run, "status") ?? "unknown",
        GetString(run, "conclusion"),
        GetString(run, "head_branch") ?? string.Empty,
        GetString(run, "head_sha") ?? string.Empty,
        GetString(run, "html_url") ?? string.Empty,
        GetDate(run, "created_at") ?? DateTimeOffset.MinValue,
        GetDate(run, "updated_at"));

    private static bool MatchesSearch(GitHubRepositorySummaryDto repository, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;
        var term = search.Trim();
        return repository.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
               || (repository.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
               || (repository.PrimaryLanguage?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool MatchesVisibility(GitHubRepositorySummaryDto repository, string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility)
            || visibility.Equals("all", StringComparison.OrdinalIgnoreCase))
            return true;
        if (visibility.Equals("private", StringComparison.OrdinalIgnoreCase))
            return repository.Private;
        if (visibility.Equals("public", StringComparison.OrdinalIgnoreCase))
            return !repository.Private;
        return true;
    }

    private static (int FileCount, long ExtractedBytes) ExtractRepositoryArchive(
        string archivePath,
        string destination,
        long maxExtractedBytes)
    {
        var count = 0;
        long extractedBytes = 0;
        using var archive = ZipFile.OpenRead(archivePath);
        var firstSegment = archive.Entries
            .Select(e => e.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;
            var relativeParts = firstSegment is not null && parts[0].Equals(firstSegment, StringComparison.Ordinal)
                ? parts.Skip(1).ToArray()
                : parts;
            if (relativeParts.Length == 0)
                continue;
            if (relativeParts.Any(part => part is "." or ".."))
                throw new InvalidDataException("GitHub archive contains an unsafe path.");

            var relative = Path.Combine(relativeParts);
            var outputPath = Path.GetFullPath(Path.Combine(destination, relative));
            var destinationRoot = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!outputPath.StartsWith(destinationRoot, PathComparison))
                throw new InvalidDataException("GitHub archive attempted to escape the workspace.");

            if (normalized.EndsWith('/'))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            count++;
            if (count > 200_000)
                throw new InvalidDataException("GitHub archive contains too many files.");
            extractedBytes += entry.Length;
            if (extractedBytes > maxExtractedBytes)
                throw new InvalidDataException("GitHub archive exceeds the extracted-size safety limit.");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var input = entry.Open();
            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }

        return (count, extractedBytes);
    }

    private void EnsureInsideWorkspace(string path)
    {
        var root = Path.GetFullPath(_workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, PathComparison) && !path.Equals(_workspaceRoot, PathComparison))
            throw new InvalidOperationException("GitHub workspace path escapes Qa:WorkspaceRoot.");
    }

    private static void ReplaceDirectory(string source, string target)
    {
        var backup = target + ".old-" + Guid.NewGuid().ToString("N");
        try
        {
            if (Directory.Exists(target))
                Directory.Move(target, backup);
            Directory.Move(source, target);
            TryDeleteDirectory(backup);
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(backup))
                Directory.Move(backup, target);
            throw;
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string? TryFirstOwner(JsonElement root)
    {
        if (!root.TryGetProperty("repositories", out var repositories))
            return null;
        foreach (var repository in repositories.EnumerateArray())
        {
            if (repository.TryGetProperty("owner", out var owner))
                return GetString(owner, "login");
        }
        return null;
    }

    private static void ValidateOwnerRepository(string owner, string repository)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
            throw new ArgumentException("GitHub repository must include owner and repository name.");
        if (owner.Contains('/') || repository.Contains('/') || owner.Contains("..") || repository.Contains(".."))
            throw new ArgumentException("GitHub repository owner/name is invalid.");
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? GetString(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static long GetInt64(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;

    private static int GetInt32(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static bool GetBoolean(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? GetDate(JsonElement node, string name)
    {
        var text = GetString(node, name);
        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    private static string SafeMessage(Exception exception)
    {
        var text = exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 300 ? text : text[..300];
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public void Dispose()
    {
        _httpClient.Dispose();
        _tokenGate.Dispose();
    }
}
