// Purpose: Provides infrastructure behavior for this application boundary.
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.ProjectIntelligence;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.ProjectIntelligence;

public sealed class RedmineProjectSourceAdapter : IProjectSourceAdapter, IDisposable
{
    private readonly ProjectIntelligenceSourceOptions.RedmineSourceOptions _o; private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    public string ProviderName => "Redmine";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.BaseUrl) && !string.IsNullOrWhiteSpace(_o.ApiKey) && (!string.IsNullOrWhiteSpace(_o.ProjectIdentifier) || _o.ProjectIdentifiers.Count > 0);
    public bool IsConfiguredFor(Guid projectId) => !string.IsNullOrWhiteSpace(_o.BaseUrl) && !string.IsNullOrWhiteSpace(_o.ApiKey) && !string.IsNullOrWhiteSpace(_o.Identifier(projectId));
    public RedmineProjectSourceAdapter(IOptions<ProjectIntelligenceSourceOptions> options) => _o = options.Value.Redmine;
    public async Task<IReadOnlyList<ProjectSourceObservation>> PullAsync(Guid projectId, CancellationToken ct)
    {
        var project = _o.Identifier(projectId);
        if (string.IsNullOrWhiteSpace(project)) throw new InvalidOperationException($"Redmine project mapping is missing for DIP project {projectId}.");
        var baseUri = Base();
        var result = new List<ProjectSourceObservation>();
        var offset = 0;
        var limit = 100;
        var status = _o.IncludeClosed ? "*" : "open";
        var maxPages = Math.Clamp(_o.MaxPages, 1, 200);
        for (var page = 0; page < maxPages; page++)
        {
            var url = new Uri(baseUri, $"issues.json?project_id={Uri.EscapeDataString(project)}&status_id={Uri.EscapeDataString(status)}&limit={limit}&offset={offset}&include=relations,journals,attachments");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Redmine-API-Key", _o.ApiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Redmine pull failed ({(int)res.StatusCode}): {Clip(raw, 800)}");
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array) return result;
            foreach (var issue in issues.EnumerateArray()) result.Add(Map(issue, baseUri));
            var count = issues.GetArrayLength();
            var total = doc.RootElement.TryGetProperty("total_count", out var t) && t.TryGetInt32(out var n) ? n : offset + count;
            if (count == 0 || offset + count >= total) return result;
            offset += count;
        }
        throw new InvalidOperationException("Redmine pull exceeded the configured page safety limit. Narrow the project/status scope or increase MaxPages deliberately.");
    }
    private static ProjectSourceObservation Map(JsonElement i, Uri baseUri)
    {
        var id = i.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture);
        var title = i.TryGetProperty("subject", out var s) ? s.GetString() ?? $"Redmine #{id}" : $"Redmine #{id}";
        var desc = i.TryGetProperty("description", out var d) ? d.GetString() : null;
        var journalNotes = JournalNotes(i);
        var body = string.IsNullOrWhiteSpace(journalNotes) ? desc : string.IsNullOrWhiteSpace(desc) ? journalNotes : desc + "\n\n--- Redmine journal ---\n" + journalNotes;
        var status = i.TryGetProperty("status", out var st) && st.TryGetProperty("name", out var sn) ? sn.GetString() ?? "Unknown" : "Unknown";
        var progress = i.TryGetProperty("done_ratio", out var dr) && dr.TryGetDecimal(out var p) ? p : (decimal?)null;
        var owner = i.TryGetProperty("assigned_to", out var a) && a.TryGetProperty("name", out var an) ? an.GetString() : null;
        var start = Date(i, "start_date");
        var due = Date(i, "due_date");
        var updated = Date(i, "updated_on") ?? DateTimeOffset.UtcNow;
        var revision = updated.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var url = new Uri(baseUri, $"issues/{id}").ToString();
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in Extract(desc)) refs.Add(x);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "IssueId", id } };
        if (i.TryGetProperty("tracker", out var tr) && tr.TryGetProperty("name", out var tn)) fields["Tracker"] = tn.GetString() ?? "";
        if (i.TryGetProperty("priority", out var pr) && pr.TryGetProperty("name", out var pn)) fields["Priority"] = pn.GetString() ?? "";
        if (i.TryGetProperty("fixed_version", out var fv) && fv.TryGetProperty("name", out var fvn)) fields["Version"] = fvn.GetString() ?? "";
        if (i.TryGetProperty("custom_fields", out var cf) && cf.ValueKind == JsonValueKind.Array) foreach (var f in cf.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var fn) ? fn.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || !f.TryGetProperty("value", out var v)) continue;
                var value = v.ValueKind == JsonValueKind.Array ? string.Join(",", v.EnumerateArray().Select(x => x.ToString())) : v.ToString();
                fields[$"Custom:{name}"] = Clip(value, 1000);
                foreach (var x in Extract(value)) refs.Add(x);
                if (name.Contains("DIP", StringComparison.OrdinalIgnoreCase) || name.Contains("Task", StringComparison.OrdinalIgnoreCase) || name.Contains("WBS", StringComparison.OrdinalIgnoreCase)) refs.Add($"DIP:{value.Trim()}");
            }
        if (i.TryGetProperty("relations", out var rel) && rel.ValueKind == JsonValueKind.Array) foreach (var r in rel.EnumerateArray())
            {
                if (r.TryGetProperty("issue_id", out var x)) refs.Add($"REDMINE#{x}");
                if (r.TryGetProperty("issue_to_id", out var y)) refs.Add($"REDMINE#{y}");
            }
        if (i.TryGetProperty("attachments",
            out var at) && at.ValueKind == JsonValueKind.Array) foreach (var x in at.EnumerateArray()) if (x.TryGetProperty("content_url",
            out var cu) && cu.ValueKind == JsonValueKind.String) refs.Add(cu.GetString()!);
        return new("Redmine", id, "issue", title, body, status, progress, owner, start, due, updated, revision, url, refs.Take(100).ToArray(), fields);
    }
    private static string? JournalNotes(JsonElement issue)
    {
        if (!issue.TryGetProperty("journals", out var journals) || journals.ValueKind != JsonValueKind.Array) return null;
        var lines = new List<string>();
        foreach (var j in journals.EnumerateArray().TakeLast(20))
        {
            var notes = j.TryGetProperty("notes", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(notes)) continue;
            var user = j.TryGetProperty("user", out var u) && u.TryGetProperty("name", out var un) ? un.GetString() : null;
            var at = j.TryGetProperty("created_on", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            lines.Add($"[{at ?? "unknown"}] {user ?? "unknown"}: {notes}");
        }
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }
    private Uri Base()
    {
        if (!IsConfigured) throw new InvalidOperationException("Redmine source requires BaseUrl, ApiKey and ProjectIdentifier/ProjectIdentifiers.");
        var u = new Uri(_o.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if (u.Scheme != Uri.UriSchemeHttps && !u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !u.Host.Equals("127.0.0.1")) throw new InvalidOperationException("Redmine BaseUrl must use HTTPS except for localhost development.");
        return u;
    }
    private static DateTimeOffset? Date(JsonElement e, string name) => e.TryGetProperty(name, out var x) && x.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(x.GetString(), out var d) ? d.ToUniversalTime() : null;
    private static IEnumerable<string> Extract(string? text) => string.IsNullOrWhiteSpace(text) ? [] : Regex.Matches(text, @"https?://[^\s<>\]\)]+|\b(?:DIP|TASK)[:#-][A-Za-z0-9_.-]+", RegexOptions.IgnoreCase).Select(x => x.Value.TrimEnd('.', ',', ';'));
    private static string Clip(string value, int max) => value[..Math.Min(max, value.Length)];
    public void Dispose() => _http.Dispose();
}
