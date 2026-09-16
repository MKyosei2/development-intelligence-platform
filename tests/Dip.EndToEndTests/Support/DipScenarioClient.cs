// Purpose: Verifies D ip Sc en ar io Cl ie nt behavior.
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

    public DipScenarioClient(TimeSpan? timeout = null)
    {
        var baseUrl = Environment.GetEnvironmentVariable("DIP_API_URL")
            ?? "http://127.0.0.1:7001";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute),
            Timeout = timeout ?? TimeSpan.FromSeconds(15)
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

    public async Task<ClaimResponse> SubmitClaimAsync(
        ProjectResponse project,
        string claimedStatus,
        decimal? claimedProgress = null,
        string subjectKey = "project")
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/claims",
            new
            {
                subjectKey,
                claimedStatus,
                claimedProgress,
                claimedBy = "e2e-reporter",
                description = "E2E project status report."
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ClaimResponse>(JsonOptions))!;
    }

    public async Task<DashboardResponse> GetDashboardAsync(Guid projectId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/dashboard/projects/{projectId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions))!;
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

    public async Task<QaRunE2EResponse> RunPythonQaAsync(ProjectResponse project)
    {
        const string fixtureWorkspace = "samples/qa-bug-zoo/python/runtime_type_error";
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/qa/runs",
            new
            {
                projectId = project.Id,
                subjectKey = "project",
                language = "Python",
                workspaceRelativePath = fixtureWorkspace,
                commitSha = (string?)null,
                runtimeExecutable = "app.py",
                runtimeArguments = Array.Empty<string>(),
                timeoutSeconds = 15,
                automaticallyReproduce = true,
                recordScreen = false,
                recordSystemAudio = false
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QaRunE2EResponse>(JsonOptions))!;
    }

    public async Task<PortabilityE2EResponse> GetPortabilityAsync(Guid projectId)
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/intelligence/projects/{projectId}/portability?targetPlatform=e2e&cpu=80&gpu=75&memory=70&storage=90&thermal=65");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PortabilityE2EResponse>(JsonOptions))!;
    }

    public async Task<RootCauseE2EResponse> AnalyzeRootCauseAsync(Guid projectId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/intelligence/root-cause",
            new
            {
                projectId,
                subjectKey = "project",
                findingText = "NullReferenceException while updating rendering state",
                stackTrace = "at Game.Render.Update()",
                diffText = "",
                logLines = new[] { "NullReferenceException" },
                includeSuggestedAction = true
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<RootCauseE2EResponse>(JsonOptions))!;
    }

    public async Task<ForecastE2EResponse> GetForecastAsync(Guid projectId)
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/intelligence/projects/{projectId}/forecast");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ForecastE2EResponse>(JsonOptions))!;
    }

    public async Task<P2DailyChangeE2EResponse> GetP2DailyChangeAsync(ProjectResponse project)
    {
        var response = await _httpClient.GetAsync($"/api/v1/p2/projects/{project.Id}/daily-change");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<P2DailyChangeE2EResponse>(JsonOptions)
            ?? throw new InvalidDataException("P2 daily-change API returned an empty response.");
    }

    public async Task<P2DecisionBriefE2EResponse> GetP2DecisionBriefAsync(ProjectResponse project)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/p2/decision-brief",
            new
            {
                projectId = project.Id,
                dependencies = (Dictionary<string, IReadOnlyList<string>>?)null
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<P2DecisionBriefE2EResponse>(JsonOptions)
            ?? throw new InvalidDataException("P2 decision-brief API returned an empty response.");
    }

    public async Task<P2HealthChangeE2EResponse> GetP2HealthChangeAsync(ProjectResponse project)
    {
        var response = await _httpClient.GetAsync($"/api/v1/p2/projects/{project.Id}/health-change");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<P2HealthChangeE2EResponse>(JsonOptions)
            ?? throw new InvalidDataException("P2 health-change API returned an empty response.");
    }

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

internal sealed record ClaimResponse(
    Guid Id,
    Guid ProjectId,
    string SubjectKey,
    string ClaimedStatus,
    decimal? ClaimedProgress,
    string ClaimedBy,
    string VerificationStatus,
    DateTimeOffset ClaimedAtUtc);

internal sealed record DashboardResponse(
    Guid ProjectId,
    string Name,
    string Repository,
    string DefaultBranch,
    decimal HealthScore,
    string TruthStatus,
    string RiskLevel,
    decimal VerifiedProgress,
    decimal? ReportedProgress,
    string EvidenceConfidence,
    int BlockerCount,
    int ContradictionCount,
    int UnverifiedClaimCount,
    string TruthSummary,
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
    string TruthStatus,
    string RiskLevel,
    decimal VerifiedProgress,
    decimal? ReportedProgress,
    string EvidenceConfidence,
    int BlockerCount,
    int ContradictionCount,
    string? LatestPipelineStatus,
    string? LatestQualityGateStatus,
    DateTimeOffset? LastEvaluatedAtUtc);

internal sealed record QaFindingE2EResponse(
    string Severity,
    string Category,
    string ReproductionStatus,
    int ReproducedAttempts,
    int TotalAttempts);

internal sealed record QaRunE2EResponse(
    Guid QaRunId,
    string Status,
    int FindingCount,
    int CriticalFindingCount,
    IReadOnlyList<QaFindingE2EResponse> Findings);

internal sealed record PortabilityE2EResponse(
    decimal Score,
    string Risk,
    IReadOnlyList<string> Blockers,
    string EvidenceJson);

internal sealed record RootCauseCandidateE2EResponse(
    string Cause,
    decimal ConfidencePercent,
    IReadOnlyList<string> Evidence,
    string? SuggestedAction);

internal sealed record RootCauseE2EResponse(
    IReadOnlyList<RootCauseCandidateE2EResponse> Candidates,
    string GroundingStatus);

internal sealed record ForecastE2EResponse(
    decimal ReleaseRiskPercent,
    string Confidence,
    DateTimeOffset? EarliestCompletionUtc,
    DateTimeOffset? LatestCompletionUtc,
    IReadOnlyList<string> Reasons);

internal sealed class P2DailyChangeE2EResponse
{
    public int NewEvidence { get; init; }
    public int NewFailures { get; init; }
    public int ResolvedOrSucceeded { get; init; }
    public int NewClaims { get; init; }
    public int NewContradictions { get; init; }
    public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();
}

internal sealed class P2DecisionPriorityE2EResponse
{
    public int Priority { get; init; }
    public string SubjectKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Why { get; init; } = string.Empty;
    public string SuggestedNextAction { get; init; } = string.Empty;
    public string EvidenceConfidence { get; init; } = string.Empty;
}

internal sealed class P2DecisionBriefE2EResponse
{
    public Guid ProjectId { get; init; }
    public string OverallStatus { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<P2DecisionPriorityE2EResponse> Priorities { get; init; } = Array.Empty<P2DecisionPriorityE2EResponse>();
}

internal sealed class P2HealthChangeE2EResponse
{
    public decimal? PreviousHealth { get; init; }
    public decimal? CurrentHealth { get; init; }
    public decimal? Difference { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
