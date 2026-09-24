// Purpose: Provides shared GitHub JSON parsing, escaping, normalization, and safe diagnostic helpers.
using System.Text.Json;

namespace Dip.Infrastructure.Integrations.GitHub;

internal static class GitHubApiJson
{
    internal static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
    }

    internal static string? OwnerOf(JsonElement repository)
    {
        if (!repository.TryGetProperty("owner", out var owner)
            || owner.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return String(owner, "login");
    }

    internal static string? String(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal static bool Bool(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is
            JsonValueKind.True
            or JsonValueKind.False
        && value.GetBoolean();

    internal static DateTimeOffset? Date(
        JsonElement element,
        string name)
    {
        var value = String(element, name);
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    internal static string Escape(string value) =>
        Uri.EscapeDataString(value);

    internal static string Short(string sha) =>
        sha[..Math.Min(12, sha.Length)];

    internal static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    internal static string Safe(string value)
    {
        var normalized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return normalized[..Math.Min(1000, normalized.Length)];
    }
}
