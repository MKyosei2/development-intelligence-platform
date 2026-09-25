// Purpose: Implements Jira Work Item Connector infrastructure responsibilities for work items.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.WorkItems;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.WorkItems;
public sealed class JiraWorkItemConnector : IExternalWorkItemConnector, IDisposable
{
    private readonly WorkItemIntegrationOptions.JiraOptions _o; private readonly HttpClient _http = new(); public string ProviderName => "Jira";
    public JiraWorkItemConnector(IOptions<WorkItemIntegrationOptions> options) { _o = options.Value.Jira; }
    public async Task<CreatedWorkItem> CreateAsync(WorkItemDraft draft, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_o.BaseUrl) || string.IsNullOrWhiteSpace(_o.Email) || string.IsNullOrWhiteSpace(_o.ApiToken) || string.IsNullOrWhiteSpace(_o.ProjectKey)) throw new InvalidOperationException("Jira integration requires BaseUrl, Email, ApiToken and ProjectKey.");
        var baseUri = new Uri(_o.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute); if (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Host != "localhost") throw new InvalidOperationException("Jira BaseUrl must use HTTPS (localhost is allowed for development).");
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "rest/api/3/issue")); req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(_o.Email + ":" + _o.ApiToken)));
        req.Content = JsonContent.Create(new { fields = new { project = new { key = _o.ProjectKey }, summary = draft.Title, issuetype = new { name = _o.IssueType }, description = new { type = "doc", version = 1, content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = draft.Body } } } } } } });
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Jira create failed ({(int)res.StatusCode}): {Clip(body)}");
        using var doc = JsonDocument.Parse(body);
        var key = doc.RootElement.TryGetProperty("key", out var k) ? k.GetString() : null;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Jira response did not contain an issue key.");
        return new(key, new Uri(baseUri, "browse/" + Uri.EscapeDataString(key)).ToString(), draft.Title);
    }
    private static string Clip(string s) => s.Length <= 1000 ? s : s[..1000]; public void Dispose() => _http.Dispose();
}
