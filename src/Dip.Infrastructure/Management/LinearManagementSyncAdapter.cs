// Purpose: Implements L in ea rM an ag em en tS yn cA da pt er infrastructure integration for m an ag em en t.
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Application.Features.Management;
using Dip.Infrastructure.WorkItems;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Management;

public sealed class LinearManagementSyncAdapter : IManagementSyncAdapter, IDisposable
{
    private readonly WorkItemIntegrationOptions.LinearOptions _o;
    private readonly HttpClient _http = new();
    public string ProviderName => "Linear";

    public LinearManagementSyncAdapter(IOptions<WorkItemIntegrationOptions> options) => _o = options.Value.Linear;

    public async Task<IReadOnlyList<ManagementExternalItem>> PullAsync(Guid projectId, CancellationToken ct)
    {
        var endpoint = Endpoint();
        var query = "query TeamIssues($teamId: String!, $after: String) { team(id: $teamId) { issues(first: 100, after: $after) { nodes { id identifier title description url updatedAt dueDate state { name type } } pageInfo { hasNextPage endCursor } } } }";
        var list = new List<ManagementExternalItem>();
        string? after = null;
        for (var page = 0; page < 100; page++)
        {
            using var req = Create(endpoint, new { query, variables = new { teamId = _o.TeamId, after } });
            using var res = await _http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Linear management pull failed ({(int)res.StatusCode}): {Clip(raw)}");
            using var doc = JsonDocument.Parse(raw);
            ThrowErrors(doc);
            var issues = doc.RootElement.GetProperty("data").GetProperty("team").GetProperty("issues");
            var nodes = issues.GetProperty("nodes");
            foreach (var issue in nodes.EnumerateArray())
            {
                var desc = issue.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : string.Empty;
                if (!ProjectMarker(desc).Equals(projectId.ToString("N"), StringComparison.OrdinalIgnoreCase)) continue;
                var key = Marker(desc);
                if (key is null) continue;
                var id = issue.GetProperty("id").GetString() ?? string.Empty;
                var status = issue.TryGetProperty("state", out var st) && st.TryGetProperty("type", out var tp) && tp.ValueKind == JsonValueKind.String && tp.GetString()?.Equals("completed", StringComparison.OrdinalIgnoreCase) == true
                    ? "Completed"
                    : issue.GetProperty("state").GetProperty("name").GetString() ?? "Open";
                var ownerRole = NormalizeOptional(Field(desc, "Owner role:"));
                DateTimeOffset? due = null;
                if (issue.TryGetProperty("dueDate", out var dd) && dd.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(dd.GetString(), out var dueAt)) due = dueAt;
                var updated = issue.TryGetProperty("updatedAt", out var u) && u.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(u.GetString(), out var at) ? at : DateTimeOffset.UtcNow;
                list.Add(new("Linear", id, key, issue.GetProperty("title").GetString() ?? string.Empty, status, ownerRole, due, updated, updated.ToUnixTimeMilliseconds().ToString(), issue.GetProperty("url").GetString() ?? string.Empty));
            }
            if (!issues.TryGetProperty("pageInfo", out var pageInfo) || !pageInfo.TryGetProperty("hasNextPage", out var hasNext) || hasNext.ValueKind != JsonValueKind.True) return list;
            after = pageInfo.TryGetProperty("endCursor", out var cursor) && cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;
            if (string.IsNullOrWhiteSpace(after)) throw new InvalidOperationException("Linear reported another page but did not provide an end cursor.");
        }
        throw new InvalidOperationException("Linear management pull exceeded the 100-page safety limit. Narrow the managed issue set before synchronizing.");
    }

    public async Task<ManagementExternalItem> UpsertAsync(Guid projectId, ManagementActionDto action, string? externalId, CancellationToken ct)
    {
        var endpoint = Endpoint();
        var description = $"<!-- dip-managed -->\n<!-- dip-project:{projectId:N} -->\n<!-- dip-key:{SafeMarker(action.Key)} -->\nOwner role: {SafeField(action.OwnerRole ?? "Unassigned")}";
        var stateId = await ResolveStateIdAsync(endpoint, action.Status, ct);
        var input = new Dictionary<string, object?>
        {
            ["title"] = action.Title,
            ["description"] = description,
            ["dueDate"] = action.DueAtUtc?.ToString("yyyy-MM-dd")
        };
        if (!string.IsNullOrWhiteSpace(stateId)) input["stateId"] = stateId;

        object payload;
        if (string.IsNullOrWhiteSpace(externalId))
        {
            input["teamId"] = _o.TeamId;
            payload = new
            {
                query = "mutation Create($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { id identifier title url updatedAt } } }",
                variables = new Dictionary<string, object?> { ["input"] = input }
            };
        }
        else
        {
            payload = new
            {
                query = "mutation Update($id: String!, $input: IssueUpdateInput!) { issueUpdate(id: $id, input: $input) { success issue { id identifier title url updatedAt } } }",
                variables = new Dictionary<string, object?> { ["id"] = externalId, ["input"] = input }
            };
        }

        using var req = Create(endpoint, payload);
        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Linear management push failed ({(int)res.StatusCode}): {Clip(raw)}");
        using var doc = JsonDocument.Parse(raw);
        ThrowErrors(doc);
        var root = doc.RootElement.GetProperty("data");
        var issue = (string.IsNullOrWhiteSpace(externalId) ? root.GetProperty("issueCreate") : root.GetProperty("issueUpdate")).GetProperty("issue");
        var id = issue.GetProperty("id").GetString() ?? throw new InvalidOperationException("Linear issue id missing.");
        var updated = issue.TryGetProperty("updatedAt", out var u) && u.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(u.GetString(), out var at) ? at : DateTimeOffset.UtcNow;
        return new("Linear", id, action.Key, issue.GetProperty("title").GetString() ?? action.Title, action.Status, action.OwnerRole, action.DueAtUtc, updated, updated.ToUnixTimeMilliseconds().ToString(), issue.GetProperty("url").GetString() ?? string.Empty);
    }

    private async Task<string?> ResolveStateIdAsync(Uri endpoint, string status, CancellationToken ct)
    {
        var query = "query TeamStates($teamId: String!) { team(id: $teamId) { states { nodes { id name type } } } }";
        using var req = Create(endpoint, new { query, variables = new { teamId = _o.TeamId } });
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) return null;
        if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("team", out var team) || team.ValueKind == JsonValueKind.Null || !team.TryGetProperty("states", out var states) || !states.TryGetProperty("nodes", out var nodes)) return null;

        var wantCompleted = IsCompleted(status);
        var wantStarted = status.Equals("InProgress", StringComparison.OrdinalIgnoreCase) || status.Equals("In Progress", StringComparison.OrdinalIgnoreCase) || status.Equals("Started", StringComparison.OrdinalIgnoreCase) || status.Equals("Active", StringComparison.OrdinalIgnoreCase);
        JsonElement? fallback = null;
        foreach (var state in nodes.EnumerateArray())
        {
            var type = state.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (wantCompleted && type.Equals("completed", StringComparison.OrdinalIgnoreCase)) return state.GetProperty("id").GetString();
            if (wantStarted && type.Equals("started", StringComparison.OrdinalIgnoreCase)) return state.GetProperty("id").GetString();
            if (!wantCompleted && !wantStarted && (type.Equals("unstarted", StringComparison.OrdinalIgnoreCase) || type.Equals("backlog", StringComparison.OrdinalIgnoreCase))) return state.GetProperty("id").GetString();
            if (!wantCompleted && fallback is null && !type.Equals("completed", StringComparison.OrdinalIgnoreCase) && !type.Equals("canceled", StringComparison.OrdinalIgnoreCase)) fallback = state;
        }
        return fallback is null ? null : fallback.Value.GetProperty("id").GetString();
    }

    private Uri Endpoint()
    {
        if (string.IsNullOrWhiteSpace(_o.ApiKey) || string.IsNullOrWhiteSpace(_o.TeamId)) throw new InvalidOperationException("Linear management sync requires ApiKey and TeamId.");
        var u = new Uri(_o.Endpoint, UriKind.Absolute);
        if (u.Scheme != Uri.UriSchemeHttps && u.Host != "localhost") throw new InvalidOperationException("Linear endpoint must use HTTPS.");
        return u;
    }

    private HttpRequestMessage Create(Uri endpoint, object payload)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, endpoint);
        r.Headers.TryAddWithoutValidation("Authorization", _o.ApiKey);
        r.Content = JsonContent.Create(payload);
        return r;
    }

    private static void ThrowErrors(JsonDocument doc) { if (doc.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0) throw new InvalidOperationException("Linear API returned errors: " + Clip(e.GetRawText())); }
    private static string ProjectMarker(string body) { const string p = "<!-- dip-project:";
     var i = body.IndexOf(p, StringComparison.OrdinalIgnoreCase);
    if (i < 0) return string.Empty;
     var e = body.IndexOf(" -->", i, StringComparison.OrdinalIgnoreCase);
     return e < 0 ? string.Empty : body[(i + p.Length)..e].Trim();
     }
    private static string? Marker(string body) { const string p = "<!-- dip-key:";
     var i = body.IndexOf(p, StringComparison.OrdinalIgnoreCase);
    if (i < 0) return null;
     var e = body.IndexOf(" -->", i, StringComparison.OrdinalIgnoreCase);
     return e < 0 ? null : body[(i + p.Length)..e].Trim();
     }
    private static string? Field(string body, string prefix) { foreach (var line in body.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
    if (line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return line.Trim()[(line.Trim().IndexOf(':') + 1)..].Trim();
     return null;
     }
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) || value.Equals("Unassigned", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
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
