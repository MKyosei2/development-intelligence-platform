// Purpose: Owns bounded GitHub archive download, extraction, exact-branch revision capture, and QA workspace replacement.
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

internal sealed class GitHubWorkspaceMaterializer
{
    private readonly GitHubAuthenticationProvider _auth;
    private readonly GitHubRepositoryClient _repositories;
    private readonly string _workspaceRoot;

    internal GitHubWorkspaceMaterializer(GitHubAuthenticationProvider auth, GitHubRepositoryClient repositories, string workspaceRoot)
    {
        _auth = auth;
        _repositories = repositories;
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<GitHubWorkspaceSyncDto> SyncWorkspaceAsync(
            Guid projectId,
            string owner,
            string repository,
            string branch,
            CancellationToken cancellationToken)
        {
            if (projectId == Guid.Empty)
                throw new ArgumentException("ProjectId is required.", nameof(projectId));
            GitHubJsonMapper.ValidateOwnerRepository(owner, repository);
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentException("Branch is required.", nameof(branch));
            _auth.EnsureConfigured();
    
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
                using var request = await _auth.CreateRequestAsync(
                    HttpMethod.Get,
                    $"{repoPath}/zipball/{Uri.EscapeDataString(branch)}",
                    cancellationToken);
                using var response = await _auth.HttpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
    
                var maxArchiveBytes = (long)Math.Clamp(_auth.Options.WorkspaceMaxArchiveMb, 32, 2048) * 1024L * 1024L;
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
                var details = await _repositories.GetRepositoryAsync(owner, repository, cancellationToken);
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
            using var request = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/commits/{Uri.EscapeDataString(branch)}",
                cancellationToken);
            using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            return GitHubJsonMapper.GetString(document.RootElement, "sha");
        }
    
        private async Task<IReadOnlyList<string>> LoadPullRequestCommitsAsync(
            string repoPath,
            int number,
            CancellationToken cancellationToken)
        {
            var results = new List<string>();
            var maxPages = Math.Clamp(_auth.Options.MaxPullRequestCommitPages, 1, 3);
            for (var page = 1; page <= maxPages; page++)
            {
                using var request = await _auth.CreateRequestAsync(
                    HttpMethod.Get,
                    $"{repoPath}/pulls/{number}/commits?per_page=100&page={page}",
                    cancellationToken);
                using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
                var count = 0;
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    count++;
                    var sha = GitHubJsonMapper.GetString(item, "sha");
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
            var maxPages = Math.Clamp(_auth.Options.MaxPullRequestFilePages, 1, 30);
            for (var page = 1; page <= maxPages; page++)
            {
                using var request = await _auth.CreateRequestAsync(
                    HttpMethod.Get,
                    $"{repoPath}/pulls/{number}/files?per_page=100&page={page}",
                    cancellationToken);
                using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
                var count = 0;
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    count++;
                    var path = GitHubJsonMapper.GetString(item, "filename") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(path))
                        continue;
                    results.Add(new GitHubPullRequestFileDto(
                        path,
                        GitHubJsonMapper.GetString(item, "status") ?? "modified",
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
            var maxPages = Math.Clamp(_auth.Options.MaxPullRequestReviewPages, 1, 50);
            for (var page = 1; page <= maxPages; page++)
            {
                using var request = await _auth.CreateRequestAsync(
                    HttpMethod.Get,
                    $"{repoPath}/pulls/{number}/reviews?per_page=100&page={page}",
                    cancellationToken);
                using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
                var count = 0;
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    count++;
                    var reviewer = item.TryGetProperty("user", out var user)
                        ? GitHubJsonMapper.GetString(user, "login") ?? "reviewer"
                        : "reviewer";
                    results.Add(new GitHubPullRequestReviewDto(
                        reviewer,
                        GitHubJsonMapper.GetString(item, "state") ?? "commented",
                        GetDate(item, "submitted_at")));
                }
                if (count < 100)
                    break;
                if (page == maxPages)
                    throw new InvalidOperationException($"Pull request #{number} review history reached the configured pagination safety limit.");
            }
            return results;
        }
    
        private async Task<HttpRequestMessage> _auth.CreateRequestAsync(
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
            if (!_auth.Options.HasGitHubAppCredentials)
            {
                if (string.IsNullOrWhiteSpace(_auth.Options.Token))
                    throw new InvalidOperationException("GitHub repository access is not configured.");
                return _auth.Options.Token.Trim();
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
                    $"app/installations/{_auth.Options.InstallationId}/access_tokens");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
                _installationToken = GitHubJsonMapper.GetString(document.RootElement, "token")
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
                iss = _auth.Options.AppId
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
            if (!string.IsNullOrWhiteSpace(_auth.Options.AppPrivateKeyBase64))
            {
                try
                {
                    return Encoding.UTF8.GitHubJsonMapper.GetString(Convert.FromBase64String(_auth.Options.AppPrivateKeyBase64.Trim()));
                }
                catch (FormatException exception)
                {
                    throw new InvalidOperationException("GitHub:AppPrivateKeyBase64 is not valid Base64.", exception);
                }
            }
    
            if (string.IsNullOrWhiteSpace(_auth.Options.AppPrivateKeyPath))
                throw new InvalidOperationException("GitHub App private key is not configured.");
            var fullPath = Path.GetFullPath(_auth.Options.AppPrivateKeyPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("GitHub App private key file was not found.", fullPath);
            return File.ReadAllText(fullPath);
        }
    
        private void _auth.EnsureConfigured()
        {
            if (!_auth.Options.HasGitHubAppCredentials && string.IsNullOrWhiteSpace(_auth.Options.Token))
                throw new InvalidOperationException("GitHub repository access is not configured.");
        }
    
        private string ResolveMode()
        {
            if (_auth.Options.HasGitHubAppCredentials)
                return "GitHubApp";
            if (!string.IsNullOrWhiteSpace(_auth.Options.Token))
                return "Token";
            return "NotConfigured";
        }
    
        private string? BuildInstallUrl() => string.IsNullOrWhiteSpace(_auth.Options.AppSlug)
            ? null
            : $"https://github.com/apps/{Uri.EscapeDataString(_auth.Options.AppSlug.Trim())}/installations/new";
    
        private static GitHubRepositorySummaryDto MapRepository(JsonElement item)
        {
            var owner = item.TryGetProperty("owner", out var ownerNode)
                ? GitHubJsonMapper.GetString(ownerNode, "login") ?? string.Empty
                : string.Empty;
            var name = GitHubJsonMapper.GetString(item, "name") ?? string.Empty;
            return new GitHubRepositorySummaryDto(
                GetInt64(item, "id"),
                owner,
                name,
                GitHubJsonMapper.GetString(item, "full_name") ?? $"{owner}/{name}",
                GitHubJsonMapper.GetString(item, "html_url") ?? string.Empty,
                GitHubJsonMapper.GetString(item, "default_branch") ?? "main",
                GetBoolean(item, "private"),
                GetBoolean(item, "archived"),
                GitHubJsonMapper.GetString(item, "language"),
                GitHubJsonMapper.GetString(item, "description"),
                GetInt32(item, "open_issues_count"),
                GetInt32(item, "stargazers_count"),
                GetInt32(item, "forks_count"),
                GetDate(item, "updated_at"));
        }
    
        private static GitHubWorkflowRunDto MapWorkflowRun(JsonElement run) => new(
            GetInt64(run, "id"),
            GitHubJsonMapper.GetString(run, "name") ?? GitHubJsonMapper.GetString(run, "display_title") ?? "Workflow",
            GitHubJsonMapper.GetString(run, "event") ?? "unknown",
            GitHubJsonMapper.GetString(run, "status") ?? "unknown",
            GitHubJsonMapper.GetString(run, "conclusion"),
            GitHubJsonMapper.GetString(run, "head_branch") ?? string.Empty,
            GitHubJsonMapper.GetString(run, "head_sha") ?? string.Empty,
            GitHubJsonMapper.GetString(run, "html_url") ?? string.Empty,
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
    
        private static async Task<JsonDocument> GitHubJsonMapper.ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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
                    return GitHubJsonMapper.GetString(owner, "login");
            }
            return null;
        }
    
        private static void GitHubJsonMapper.ValidateOwnerRepository(string owner, string repository)
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
    
        private static string? GitHubJsonMapper.GetString(JsonElement node, string name)
        {
            if (!node.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;
            return value.ValueKind == JsonValueKind.String ? value.GitHubJsonMapper.GetString() : value.ToString();
        }
    
        private static long GetInt64(JsonElement node, string name) =>
            node.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;
    
        private static int GetInt32(JsonElement node, string name) =>
            node.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;
    
        private static bool GetBoolean(JsonElement node, string name) =>
            node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    
        private static DateTimeOffset? GetDate(JsonElement node, string name)
        {
            var text = GitHubJsonMapper.GetString(node, name);
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
            _auth.HttpClient.Dispose();
            _tokenGate.Dispose();
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
            using var request = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/commits/{Uri.EscapeDataString(branch)}",
                cancellationToken);
            using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            return GitHubJsonMapper.GetString(document.RootElement, "sha");
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

    private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

    private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
}
