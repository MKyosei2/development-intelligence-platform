// Purpose: Implements Jira Management Sync Adapter infrastructure behavior for management.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.Management;
using Dip.Infrastructure.WorkItems;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Management;

public sealed class JiraManagementSyncAdapter : IManagementSyncAdapter, IDisposable
{
    private readonly WorkItemIntegrationOptions.JiraOptions _o;
    private readonly HttpClient _http = new();
    public string ProviderName => "Jira";

    public JiraManagementSyncAdapter(IOptions<WorkItemIntegrationOptions> options) => _o = options.Value.Jira;

    public async Task<IReadOnlyList<ManagementExternalItem>> PullAsync(Guid projectId, CancellationToken ct)
    {
        var baseUri = Base();
        var projectLabel = ProjectLabel(projectId);
        var jql = Uri.EscapeDataString($"project = {_o.ProjectKey} AND labels = dip-managed AND labels = \"{projectLabel}\"");
        var list = new List<ManagementExternalItem>();
        var startAt = 0;
        for (var page = 0; page < 100; page++)
        {
            using var req = Request(HttpMethod.Get, new Uri(baseUri, $"rest/api/3/search?jql={jql}&fields=summary,status,duedate,updated,description&maxResults=100&startAt={startAt}"));
            using var res = await _http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"Jira management pull failed ({(int)res.StatusCode}): {Clip(raw)}");

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array) return list;
            var pageCount = issues.GetArrayLength();
            foreach (var issue in issues.EnumerateArray())
            {
                var id = issue.GetProperty("key").GetString() ?? string.Empty;
                var f = issue.GetProperty("fields");
                var rawTitle = f.GetProperty("summary").GetString() ?? string.Empty;
                var (key, title) = ParseSummary(rawTitle, id);
                var status = f.TryGetProperty("status", out var st) && st.TryGetProperty("name", out var sn) ? sn.GetString() ?? "Open" : "Open";
                DateTimeOffset? due = null;
                if (f.TryGetProperty("duedate", out var dd) && dd.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(dd.GetString(), out var dueAt)) due = dueAt;
                var updated = f.TryGetProperty("updated", out var up) && up.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(up.GetString(), out var at) ? at : DateTimeOffset.UtcNow;
                var description = f.TryGetProperty("description", out var desc) ? AdfText(desc) : string.Empty;
                var ownerRole = NormalizeOptional(Field(description, "Owner role:"));
                list.Add(new("Jira", id, key, title, status, ownerRole, due, updated, updated.ToUnixTimeMilliseconds().ToString(), new Uri(baseUri, "browse/" + Uri.EscapeDataString(id)).ToString()));
            }
            var total = 0;
            var hasTotal = doc.RootElement.TryGetProperty("total", out var totalEl) && totalEl.TryGetInt32(out total);
            var isLast = doc.RootElement.TryGetProperty("isLast", out var lastEl) && lastEl.ValueKind == JsonValueKind.True;
            if (pageCount == 0 || isLast || (hasTotal && startAt + pageCount >= total)) return list;
            startAt += pageCount;
        }
        throw new InvalidOperationException("Jira management pull exceeded the 100-page safety limit. Narrow the managed issue set before synchronizing.");
    }

    public async Task<ManagementExternalItem> UpsertAsync(Guid projectId, ManagementActionDto action, string? externalId, CancellationToken ct)
    {
        var baseUri = Base();
        var summary = $"[DIP:{SafeKey(action.Key)}] {action.Title}";
        var description = Adf($"DIP management action {SafeField(action.Key)}\nOwner role: {SafeField(action.OwnerRole ?? "Unassigned")}");
        var labels = new[] { "dip-managed", ProjectLabel(projectId) };
        var fields = new { summary, duedate = action.DueAtUtc?.ToString("yyyy-MM-dd"), labels, description };
        string id;

        if (string.IsNullOrWhiteSpace(externalId))
        {
            using var req = Request(HttpMethod.Post, new Uri(baseUri, "rest/api/3/issue"));
            req.Content = JsonContent.Create(new
            {
                fields = new
                {
                    project = new { key = _o.ProjectKey },
                    issuetype = new { name = _o.IssueType },
                    summary,
                    duedate = action.DueAtUtc?.ToString("yyyy-MM-dd"),
                    labels,
                    description
                }
            });
            using var res = await _http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Jira management create failed ({(int)res.StatusCode}): {Clip(raw)}");
            using var doc = JsonDocument.Parse(raw);
            id = doc.RootElement.GetProperty("key").GetString() ?? throw new InvalidOperationException("Jira issue key missing.");
        }
        else
        {
            id = externalId;
            using var req = Request(HttpMethod.Put, new Uri(baseUri, "rest/api/3/issue/" + Uri.EscapeDataString(id)));
            req.Content = JsonContent.Create(new { fields });
            using var res = await _http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Jira management update failed ({(int)res.StatusCode}): {Clip(raw)}");
        }

        await TryTransitionAsync(baseUri, id, action.Status, ct);
        var now = DateTimeOffset.UtcNow;
        return new("Jira", id, action.Key, action.Title, action.Status, action.OwnerRole, action.DueAtUtc, now, now.ToUnixTimeMilliseconds().ToString(), new Uri(baseUri, "browse/" + Uri.EscapeDataString(id)).ToString());
    }

    private async Task TryTransitionAsync(Uri baseUri, string id, string status, CancellationToken ct)
    {
        var wantDone = IsCompleted(status);
        using var get = Request(HttpMethod.Get, new Uri(baseUri, $"rest/api/3/issue/{Uri.EscapeDataString(id)}/transitions"));
        using var res = await _http.SendAsync(get, ct);
        if (!res.IsSuccessStatusCode) return;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("transitions", out var values)) return;
        JsonElement? match = null;
        foreach (var t in values.EnumerateArray())
        {
            var name = t.GetProperty("name").GetString() ?? string.Empty;
            var category = t.TryGetProperty("to", out var to) && to.TryGetProperty("statusCategory", out var cat) && cat.TryGetProperty("key", out var k) ? k.GetString() ?? string.Empty : string.Empty;
            if (wantDone && (category.Equals("done", StringComparison.OrdinalIgnoreCase) || name.Contains("done", StringComparison.OrdinalIgnoreCase) || name.Contains("close", StringComparison.OrdinalIgnoreCase))) { match = t; break; }
            if (!wantDone && (category.Equals("new", StringComparison.OrdinalIgnoreCase) || name.Contains("open", StringComparison.OrdinalIgnoreCase) || name.Contains("to do", StringComparison.OrdinalIgnoreCase))) { match = t; break; }
        }
        if (match is null) return;
        using var post = Request(HttpMethod.Post, new Uri(baseUri, $"rest/api/3/issue/{Uri.EscapeDataString(id)}/transitions"));
        post.Content = JsonContent.Create(new { transition = new { id = match.Value.GetProperty("id").GetString() } });
        using var _ = await _http.SendAsync(post, ct);
    }

    private Uri Base()
    {
        if (string.IsNullOrWhiteSpace(_o.BaseUrl) || string.IsNullOrWhiteSpace(_o.Email) || string.IsNullOrWhiteSpace(_o.ApiToken) || string.IsNullOrWhiteSpace(_o.ProjectKey))
            throw new InvalidOperationException("Jira management sync requires BaseUrl, Email, ApiToken and ProjectKey.");
        var u = new Uri(_o.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if (u.Scheme != Uri.UriSchemeHttps && u.Host != "localhost") throw new InvalidOperationException("Jira BaseUrl must use HTTPS.");
        return u;
    }

    private HttpRequestMessage Request(HttpMethod method, Uri uri)
    {
        var r = new HttpRequestMessage(method, uri);
        r.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(_o.Email + ":" + _o.ApiToken)));
        r.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return r;
    }

    private static object Adf(string text) => new { type = "doc", version = 1, content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text } } } } };

    private static string AdfText(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return string.Empty;
        var values = new List<string>();
        CollectText(element, values);
        return string.Join("\n", values);
    }

    private static void CollectText(JsonElement element, List<string> values)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString())) values.Add(text.GetString()!);
            foreach (var property in element.EnumerateObject()) if (!property.NameEquals("text")) CollectText(property.Value, values);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) CollectText(child, values);
        }
    }

    private static string? Field(string text, string prefix)
    {
        foreach (var line in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
            if (line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return line.Trim()[(line.Trim().IndexOf(':') + 1)..].Trim();
        return null;
    }

    private static string ProjectLabel(Guid projectId) => $"dip-project-{projectId:N}";
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) || value.Equals("Unassigned", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
    private static (string Key, string Title) ParseSummary(string summary, string fallback) { if (!summary.StartsWith("[DIP:", StringComparison.OrdinalIgnoreCase)) return ($"jira-{fallback}", summary);
     var end = summary.IndexOf(']');
    if (end < 6) return ($"jira-{fallback}", summary);
     return (summary[5..end], summary[(end + 1)..].Trim());
     }
    private static string SafeKey(string value)
    {
        var safe = SafeField(value).Replace("]", "", StringComparison.Ordinal).Replace("[", "", StringComparison.Ordinal);
        return safe[..Math.Min(120, safe.Length)];
    }
    private static string SafeField(string value)
    {
        var chars = new List<char>(Math.Min(value.Length, 200));
        foreach (var ch in value)
        {
            if (char.IsControl(ch)) continue;
            chars.Add(ch);
            if (chars.Count >= 200) break;
        }
        return new string(chars.ToArray()).Trim();
    }
    private static bool IsCompleted(string status) => status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || status.Equals("Done", StringComparison.OrdinalIgnoreCase) || status.Equals("Closed", StringComparison.OrdinalIgnoreCase) || status.Equals("Resolved", StringComparison.OrdinalIgnoreCase);
    private static string Clip(string s) => s.Length <= 1500 ? s : s[..1500];
    public void Dispose() => _http.Dispose();
}
