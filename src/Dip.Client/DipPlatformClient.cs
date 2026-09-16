// Purpose: Implements D ip Pl at fo rm Cl ie nt client behavior.
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dip.Client;

public sealed record ProjectListItem(
    Guid Id,
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    string? LifecycleState);

public sealed record CapabilityListItem(
    string Id,
    string Category,
    string DisplayName,
    string Description,
    bool ProjectScoped,
    string? ReadEndpointTemplate,
    string ImplementationState,
    string VerificationState);

public sealed record CapabilityReadResult(
    bool Success,
    int StatusCode,
    string Endpoint,
    string Content,
    string? Error);

public sealed class DipPlatformClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient _http;

    public DipPlatformClient(string baseUrl = "http://127.0.0.1:5080/")
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ProjectListItem>> GetProjectsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<IReadOnlyList<ProjectListItem>>("api/projects", JsonOptions, ct)
           ?? Array.Empty<ProjectListItem>();

    public async Task<IReadOnlyList<CapabilityListItem>> GetCapabilitiesAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<IReadOnlyList<CapabilityListItem>>("api/v1/capabilities", JsonOptions, ct)
           ?? Array.Empty<CapabilityListItem>();

    public async Task<CapabilityReadResult> ReadCapabilityAsync(
        CapabilityListItem capability,
        Guid? projectId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(capability.ReadEndpointTemplate))
            return new(false, 0, string.Empty, string.Empty, "This capability does not expose a read endpoint.");

        if (capability.ProjectScoped && (!projectId.HasValue || projectId.Value == Guid.Empty))
            return new(false, 0, capability.ReadEndpointTemplate, string.Empty, "Select a project first.");

        var endpoint = capability.ReadEndpointTemplate.Replace(
            "{projectId}",
            projectId?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        try
        {
            using var response = await _http.GetAsync(endpoint, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            var formatted = PrettyJson(raw);
            return new(
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                endpoint,
                formatted,
                response.IsSuccessStatusCode ? null : response.ReasonPhrase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, 0, endpoint, string.Empty, ex.Message);
        }
    }

    private static string PrettyJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "(empty response)";

        try
        {
            using var document = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public void Dispose() => _http.Dispose();
}
