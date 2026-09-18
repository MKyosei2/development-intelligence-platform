// Purpose: Integrates external or runtime capabilities through G it Hu bM an ag em en tS yn cA da pt er.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Management;
using Dip.Infrastructure.GitHub;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Management;

public sealed class GitHubManagementSyncAdapter : IManagementSyncAdapter, IDisposable
{
    private readonly GitHubOptions _options;
    private readonly IProjectRepository _projects;
    private readonly HttpClient _http = new();
    public string ProviderName => "GitHub";

    public GitHubManagementSyncAdapter(IOptions<GitHubOptions> options, IProjectRepository projects)
    {
        _options = options.Value;
        _projects = projects;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(_options.ProductHeader) ? "DIP" : _options.ProductHeader);
        if (!string.IsNullOrWhiteSpace(_options.Token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<IReadOnlyList<ManagementExternalItem>> PullAsync(Guid projectId, CancellationToken ct)
    {
        var (owner, repo) = await Repository(projectId, ct);
        var baseUri = ApiBase();
        var list = new List<ManagementExternalItem>();
        for (var page = 1; page <= 100; page++)
        {
            var url = new Uri(baseUri, $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/issues?state=all&labels=dip-managed&per_page=100&page={page}");
            using var res = await _http.GetAsync(url, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"GitHub management pull failed ({(int)res.StatusCode}): {Clip(raw)}");

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("GitHub management pull did not return an issue array.");
            var pageCount = 0;
            foreach (var issue in doc.RootElement.EnumerateArray())
            {
                pageCount++;
                if (issue.TryGetProperty("pull_request", out _)) continue;
                var number = issue.GetProperty("number").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
                var title = issue.GetProperty("title").GetString() ?? string.Empty;
                var body = issue.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? string.Empty : string.Empty;
                if (!ProjectMarker(body).Equals(projectId.ToString("N"), StringComparison.OrdinalIgnoreCase)) continue;
                var key = Marker(body) ?? $"github-{number}";
                var state = issue.GetProperty("state").GetString()?.Equals("closed", StringComparison.OrdinalIgnoreCase) == true ? "Completed" : "Open";
                var ownerRole = NormalizeOptional(Field(body, "Owner role:"));
                DateTimeOffset? due = null;
                var dueText = Field(body, "Due:");
                if (!string.IsNullOrWhiteSpace(dueText) && !dueText.Equals("n/a", StringComparison.OrdinalIgnoreCase) && DateTimeOffset.TryParse(dueText, out var dueAt)) due = dueAt;
                var updated = issue.TryGetProperty("updated_at", out var u) && u.TryGetDateTimeOffset(out var at) ? at : DateTimeOffset.UtcNow;
                var html = issue.GetProperty("html_url").GetString() ?? string.Empty;
                list.Add(new("GitHub", number, key, title, state, ownerRole, due, updated, updated.ToUnixTimeSeconds().ToString(), html));
            }
            if (pageCount < 100) return list;
        }
        throw new InvalidOperationException("GitHub management pull exceeded the 10,000 issue safety limit. Narrow the managed issue set before synchronizing.");
    }

    public async Task<ManagementExternalItem> UpsertAsync(Guid projectId, ManagementActionDto action, string? externalId, CancellationToken ct)
    {
        var (owner, repo) = await Repository(projectId, ct);
        var baseUri = ApiBase();
        var closed = IsCompleted(action.Status);
        var body = $"<!-- dip-managed -->\n<!-- dip-project:{projectId:N} -->\n<!-- dip-key:{SafeMarker(action.Key)} -->\nOwner role: {SafeField(action.OwnerRole ?? "Unassigned")}\nDue: {(action.DueAtUtc?.ToString("yyyy-MM-dd") ?? "n/a")}";
        HttpMethod method;
        Uri url;
        object payload;
        if (!string.IsNullOrWhiteSpace(externalId) && int.TryParse(externalId, out var number))
        {
            method = HttpMethod.Patch;
            url = new Uri(baseUri, $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/issues/{number}");
            payload = new { title = action.Title, body, state = closed ? "closed" : "open" };
        }
        else
        {
            method = HttpMethod.Post;
            url = new Uri(baseUri, $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/issues");
            payload = new { title = action.Title, body, labels = new[] { "dip-managed" } };
        }

        using var req = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub management push failed ({(int)res.StatusCode}): {Clip(raw)}");
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var id = root.GetProperty("number").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var updated = root.TryGetProperty("updated_at", out var u) && u.TryGetDateTimeOffset(out var at) ? at : DateTimeOffset.UtcNow;
        return new("GitHub", id, action.Key, root.GetProperty("title").GetString() ?? action.Title, closed ? "Completed" : "Open", action.OwnerRole, action.DueAtUtc, updated, updated.ToUnixTimeSeconds().ToString(), root.GetProperty("html_url").GetString() ?? string.Empty);
    }

    private async Task<(string Owner, string Repo)> Repository(Guid projectId, CancellationToken ct)
    {
        var p = await _projects.GetByIdAsync(projectId, ct) ?? throw new KeyNotFoundException("Project not found.");
        if (!Uri.TryCreate(p.RepositoryUrl, UriKind.Absolute, out var uri)) throw new InvalidOperationException("Project repository URL is invalid.");
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new InvalidOperationException("GitHub repository URL must contain owner/name.");
        var repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        return (parts[0], repo);
    }

    private Uri ApiBase()
    {
        var u = new Uri(string.IsNullOrWhiteSpace(_options.ApiBaseUrl) ? "https://api.github.com/" : _options.ApiBaseUrl, UriKind.Absolute);
        if (u.Scheme != Uri.UriSchemeHttps && u.Host != "localhost") throw new InvalidOperationException("GitHub API base URL must use HTTPS.");
        return u.AbsoluteUri.EndsWith('/') ? u : new Uri(u.AbsoluteUri + "/");
    }

    private static string ProjectMarker(string body)
    {
        const string prefix = "<!-- dip-project:";
        var i = body.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return string.Empty;
        var e = body.IndexOf(" -->", i, StringComparison.OrdinalIgnoreCase);
        return e < 0 ? string.Empty : body[(i + prefix.Length)..e].Trim();
    }

    private static string? Marker(string body)
    {
        const string prefix = "<!-- dip-key:";
        var i = body.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var e = body.IndexOf(" -->", i, StringComparison.OrdinalIgnoreCase);
        return e < 0 ? null : body[(i + prefix.Length)..e].Trim();
    }

    private static string? Field(string body, string prefix)
    {
        foreach (var line in body.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
            if (line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return line.Trim()[(line.Trim().IndexOf(':') + 1)..].Trim();
        return null;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) || value.Equals("Unassigned", StringComparison.OrdinalIgnoreCase) || value.Equals("n/a", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
    private static bool IsCompleted(string status) => status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || status.Equals("Done", StringComparison.OrdinalIgnoreCase) || status.Equals("Closed", StringComparison.OrdinalIgnoreCase) || status.Equals("Resolved", StringComparison.OrdinalIgnoreCase);
    private static string SafeMarker(string value) => SafeSingleLine(value, 200).Replace("--", "", StringComparison.Ordinal).Replace("<", "", StringComparison.Ordinal).Replace(">", "", StringComparison.Ordinal);
    private static string SafeField(string value) => SafeSingleLine(value, 200);
    private static string SafeSingleLine(string value, int maxLength)
    {
        var chars = new List<char>(Math.Min(value.Length, maxLength));
        foreach (var ch in value)
        {
            if (char.IsControl(ch)) continue;
            chars.Add(ch);
            if (chars.Count >= maxLength) break;
        }
        return new string(chars.ToArray()).Trim();
    }
    private static string Clip(string s) => s.Length <= 1500 ? s : s[..1500];
    public void Dispose() => _http.Dispose();
}
