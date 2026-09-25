// Purpose: Implements Linear Work Item Connector infrastructure behavior for this repository area.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Application.Abstractions.WorkItems;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.WorkItems;
public sealed class LinearWorkItemConnector : IExternalWorkItemConnector, IDisposable
{
    private readonly WorkItemIntegrationOptions.LinearOptions _o; private readonly HttpClient _http = new(); public string ProviderName => "Linear";
    public LinearWorkItemConnector(IOptions<WorkItemIntegrationOptions> options) { _o = options.Value.Linear; }
    public async Task<CreatedWorkItem> CreateAsync(WorkItemDraft draft, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_o.ApiKey) || string.IsNullOrWhiteSpace(_o.TeamId)) throw new InvalidOperationException("Linear integration requires ApiKey and TeamId.");
        var endpoint = new Uri(_o.Endpoint, UriKind.Absolute);
        if (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Host != "localhost") throw new InvalidOperationException("Linear endpoint must use HTTPS (localhost is allowed for development).");
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.TryAddWithoutValidation("Authorization", _o.ApiKey);
        req.Content = JsonContent.Create(new { query = "mutation IssueCreate($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { id identifier url title } } }", variables = new { input = new { teamId = _o.TeamId, title = draft.Title, description = draft.Body } } });
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Linear create failed ({(int)res.StatusCode}): {Clip(body)}");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("errors", out var errors)) throw new InvalidOperationException("Linear API returned errors: " + Clip(errors.GetRawText()));
        var issue = doc.RootElement.GetProperty("data").GetProperty("issueCreate").GetProperty("issue");
        var id = issue.TryGetProperty("identifier", out var ident) ? ident.GetString() : issue.GetProperty("id").GetString();
        var url = issue.GetProperty("url").GetString();
        return new(id ?? throw new InvalidOperationException("Linear issue id missing."), url ?? throw new InvalidOperationException("Linear issue url missing."), draft.Title);
    }
    private static string Clip(string s) => s.Length <= 1000 ? s : s[..1000]; public void Dispose() => _http.Dispose();
}
