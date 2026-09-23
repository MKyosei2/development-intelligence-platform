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

    private static (int FileCount, long ExtractedBytes) ExtractRepositoryArchive(
        string archivePath,
        string destination,
        long maxExtractedBytes)
    {
        var count = 0;
        long extractedBytes = 0;
        using var archive = ZipFile.OpenRead(archivePath);
        var firstSegment = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault())
            .FirstOrDefault(segment => !string.IsNullOrWhiteSpace(segment));

        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            var relativeParts = firstSegment is not null
                                && parts[0].Equals(firstSegment, StringComparison.Ordinal)
                ? parts.Skip(1).ToArray()
                : parts;
            if (relativeParts.Length == 0)
                continue;
            if (relativeParts.Any(part => part is "." or ".."))
                throw new InvalidDataException("GitHub archive contains an unsafe path.");

            var relative = Path.Combine(relativeParts);
            var outputPath = Path.GetFullPath(Path.Combine(destination, relative));
            var destinationRoot =
                Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
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
            using var output = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            input.CopyTo(output);
        }

        return (count, extractedBytes);
    }

    private void EnsureInsideWorkspace(string path)
    {
        var root =
            Path.GetFullPath(_workspaceRoot).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, PathComparison)
            && !path.Equals(_workspaceRoot, PathComparison))
        {
            throw new InvalidOperationException(
                "GitHub workspace path escapes Qa:WorkspaceRoot.");
        }
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
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup is best-effort; the primary workspace operation has already completed or failed.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Cleanup is best-effort; the primary workspace operation has already completed or failed.
        }
    }
}
