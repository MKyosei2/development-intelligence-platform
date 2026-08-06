using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Dip.EndToEndTests.Support;

internal sealed class DipScenarioClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public DipScenarioClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("DIP_API_URL")
            ?? "http://127.0.0.1:7001";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<ProjectResponse> CreateProjectAsync(string scenario)
    {
        var id = Guid.NewGuid().ToString("N");
        var response = await _httpClient.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"E2E {scenario} {id}",
                repositoryUrl = $"https://github.com/example/e2e-{id}",
                defaultBranch = "main",
                releaseDateUtc = (DateTimeOffset?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    public async Task<CollectorResponse> SubmitReportAsync(
        ProjectResponse project,
        object[] metrics,
        object[] issues)
    {
        var now = DateTimeOffset.UtcNow;
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/collector-reports",
            new
            {
                schemaVersion = "1.0",
                reportId = $"report-{Guid.NewGuid():N}",
                externalRunId = $"run-{Guid.NewGuid():N}",
                attemptNumber = 1,
                collectorType = "e2e",
                collectorVersion = "1.0.0",
                projectId = project.Id,
                repository = project.RepositoryUrl,
                commitSha = "0123456789abcdef0123456789abcdef01234567",
                branch = project.DefaultBranch,
                startedAt = now.AddSeconds(-2),
                finishedAt = now,
                status = "Succeeded",
                metrics,
                issues
            });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CollectorResponse>(JsonOptions))!;
    }

    public async Task<DashboardResponse> WaitForDashboardAsync(
        Guid projectId,
        string expectedStatus,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(45));
        DashboardResponse? last = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var response = await _httpClient.GetAsync($"/api/v1/dashboard/projects/{projectId}");
            if (response.IsSuccessStatusCode)
            {
                last = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);
                if (string.Equals(last?.QualityGateStatus, expectedStatus, StringComparison.Ordinal))
                    return last!;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Dashboard did not reach '{expectedStatus}'. Last status: '{last?.QualityGateStatus ?? "none"}'.");
    }

    public async Task<IReadOnlyList<DashboardSummaryResponse>> GetDashboardProjectsAsync() =>
        await _httpClient.GetFromJsonAsync<List<DashboardSummaryResponse>>(
            "/api/v1/dashboard/projects",
            JsonOptions) ?? [];

    public async Task<HttpResponseMessage> SendSignedGitHubWebhookAsync(
        string deliveryId,
        string eventName,
        string payload,
        string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), bytes))
            .ToLowerInvariant();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/webhooks/github");
        request.Headers.Add("X-GitHub-Delivery", deliveryId);
        request.Headers.Add("X-GitHub-Event", eventName);
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        return await _httpClient.SendAsync(request);
    }

    public void Dispose() => _httpClient.Dispose();
}

internal sealed record ProjectResponse(
    Guid Id,
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    decimal HealthScore,
    DateTimeOffset? ReleaseDateUtc);

internal sealed record CollectorResponse(Guid PipelineRunId, bool WasDuplicate);

internal sealed record DashboardResponse(
    Guid ProjectId,
    string Name,
    string Repository,
    string DefaultBranch,
    decimal HealthScore,
    string RiskLevel,
    Guid? LatestPipelineRunId,
    string? LatestPipelineStatus,
    DateTimeOffset? LatestPipelineFinishedAtUtc,
    string? QualityGateStatus,
    decimal? QualityGateScore,
    int CriticalIssueCount,
    int WarningIssueCount,
    IReadOnlyList<string> QualityGateReasons);

internal sealed record DashboardSummaryResponse(
    Guid ProjectId,
    string Name,
    string Repository,
    string DefaultBranch,
    decimal HealthScore,
    string RiskLevel,
    string? LatestPipelineStatus,
    string? LatestQualityGateStatus,
    DateTimeOffset? LastEvaluatedAtUtc);
