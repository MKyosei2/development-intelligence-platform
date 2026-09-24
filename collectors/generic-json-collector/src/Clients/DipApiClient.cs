// Purpose: Submits collector reports to the DIP API and normalizes success and error responses.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GenericJsonCollector.Models;

namespace GenericJsonCollector.Clients;

public sealed record DipSubmissionResult(
    bool IsSuccess,
    int StatusCode,
    Guid? PipelineRunId,
    string? PipelineStatus,
    bool WasDuplicate,
    string? QualityGateStatus,
    string? ErrorMessage);

public sealed class DipApiClient
{
    private const string SubmitPath = "api/v1/collector-reports";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly HttpClient _httpClient;

    static DipApiClient()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public DipApiClient(HttpClient httpClient, string? apiKey = null)
    {
        _httpClient = httpClient;

        if (!string.IsNullOrWhiteSpace(apiKey))
            _httpClient.DefaultRequestHeaders.Add("X-DIP-API-Key", apiKey.Trim());

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "dip-generic-json-collector/1.0");
    }

    public async Task<DipSubmissionResult> SubmitAsync(
        CollectorReport report,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            SubmitPath,
            report,
            JsonOptions,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new(
                false,
                (int)response.StatusCode,
                null,
                null,
                false,
                null,
                ExtractError(body, response.StatusCode));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new(
                false,
                (int)response.StatusCode,
                null,
                null,
                false,
                null,
                "The API returned an empty response.");
        }

        ApiCollectorReportResult? result;
        try
        {
            result = JsonSerializer.Deserialize<ApiCollectorReportResult>(
                body,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            return new(
                false,
                (int)response.StatusCode,
                null,
                null,
                false,
                null,
                $"The API returned invalid JSON: {exception.Message}");
        }

        if (result is null)
            return new(false, (int)response.StatusCode, null, null, false, null,
                "The API returned an empty response.");

        return new(
            true,
            (int)response.StatusCode,
            result.PipelineRunId,
            result.PipelineStatus,
            result.WasDuplicate,
            result.QualityGate?.Status,
            null);
    }

    private static string ExtractError(string body, HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
            return $"HTTP {(int)statusCode} {statusCode}";

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            foreach (var name in new[] { "detail", "title", "message" })
            {
                if (root.TryGetProperty(name, out var property)
                    && property.ValueKind == JsonValueKind.String)
                    return property.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // Intentionally ignored to preserve the existing best-effort fallback behavior.
        }

        return body.Length <= 2000 ? body : body[..2000];
    }

    private sealed record ApiCollectorReportResult(
        Guid PipelineRunId,
        string PipelineStatus,
        bool WasDuplicate,
        ApiQualityGateResult? QualityGate);

    private sealed record ApiQualityGateResult(string Status);
}
