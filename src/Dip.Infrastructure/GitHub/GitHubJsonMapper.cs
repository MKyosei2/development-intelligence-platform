// Purpose: Maps GitHub JSON payloads and centralizes safe repository/response parsing helpers.
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

internal static class GitHubJsonMapper
{
    internal static GitHubRepositorySummaryDto MapRepository(JsonElement item)
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

    internal static GitHubWorkflowRunDto MapWorkflowRun(JsonElement run) => new(
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

    internal static bool MatchesSearch(GitHubRepositorySummaryDto repository, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;
        var term = search.Trim();
        return repository.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
               || (repository.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
               || (repository.PrimaryLanguage?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    internal static bool MatchesVisibility(GitHubRepositorySummaryDto repository, string? visibility)
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

    internal static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    internal static string? TryFirstOwner(JsonElement root)
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

    internal static void ValidateOwnerRepository(string owner, string repository)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
            throw new ArgumentException("GitHub repository must include owner and repository name.");
        if (owner.Contains('/') || repository.Contains('/') || owner.Contains("..") || repository.Contains(".."))
            throw new ArgumentException("GitHub repository owner/name is invalid.");
    }

    internal static string EnsureTrailingSlash(string value) =>
            value.EndsWith('/') ? value : value + "/";

    internal static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string? GetString(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    internal static long GetInt64(JsonElement node, string name) =>
            node.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;

    internal static int GetInt32(JsonElement node, string name) =>
            node.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

    internal static bool GetBoolean(JsonElement node, string name) =>
            node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    internal static DateTimeOffset? GetDate(JsonElement node, string name)
    {
        var text = GetString(node, name);
        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    internal static string SafeMessage(Exception exception)
    {
        var text = exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 300 ? text : text[..300];
    }

    internal static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
