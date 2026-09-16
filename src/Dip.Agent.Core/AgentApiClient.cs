// Purpose: Implements A ge nt Ap iC li en t agent behavior.
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Dip.Agent.Core;

public sealed class AgentApiClient : IDisposable
{
    private readonly AgentOptions _options;
    private readonly HttpClient _http;

    public AgentApiClient(AgentOptions options)
    {
        _options = AgentOptions.Validate(options);
        _http = new HttpClient { BaseAddress = _options.ApiBaseUrl, Timeout = TimeSpan.FromMinutes(10) };
        if (!string.IsNullOrWhiteSpace(_options.BearerToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken.Trim());
    }

    public async Task SendAsync(IReadOnlyList<LocalFileObservation> observations, CancellationToken ct)
    {
        if (observations.Count == 0) return;
        var request = new LocalFileBatchRequest(_options.AgentId, _options.RootId, observations);
        using var response = await _http.PostAsJsonAsync(
            $"api/v1/projects/{_options.ProjectId}/collector/file-events/batch",
            request,
            ct);
        await EnsureSuccessAsync(response, "collector", ct);
    }

    public async Task SendToolEventsAsync(IReadOnlyList<ToolEventObservation> events, CancellationToken ct)
    {
        if (events.Count == 0) return;
        var request = new ProductionToolBatchRequest(_options.AgentId, events);
        using var response = await _http.PostAsJsonAsync(
            $"api/v1/projects/{_options.ProjectId}/production/events/batch",
            request,
            ct);
        await EnsureSuccessAsync(response, "production events", ct);
    }

    public async Task SendMediaAsync(QueuedMediaUpload item, CancellationToken ct)
    {
        await using var stream = new FileStream(item.SpoolPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(_options.AgentId), "agentId");
        content.Add(new StringContent(item.EventId), "eventId");
        content.Add(new StringContent(item.Source.ToString()), "source");
        content.Add(new StringContent(item.Kind.ToString()), "kind");
        content.Add(new StringContent(item.RelativePath), "relativePath");
        content.Add(new StringContent(item.Sha256), "sha256");
        using var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(item.ContentType);
        content.Add(file, "file", Path.GetFileName(item.RelativePath));

        using var response = await _http.PostAsync(
            $"api/v1/projects/{_options.ProjectId}/production/media",
            content,
            ct);
        await EnsureSuccessAsync(response, "production media", ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"DIP {operation} API returned {(int)response.StatusCode}: {body}");
    }

    public void Dispose() => _http.Dispose();
}
