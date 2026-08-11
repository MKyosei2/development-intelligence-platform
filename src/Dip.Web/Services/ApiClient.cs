using System.Net;
using System.Net.Http.Json;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Web.Services;

public sealed record CreateClaimRequest(
    string SubjectKey,
    string ClaimedStatus,
    decimal? ClaimedProgress,
    string? ClaimedBy,
    string? Description);

public sealed class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ProjectDashboardSummary>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<
                   IReadOnlyList<ProjectDashboardSummary>>(
                   "api/v1/dashboard/projects",
                   cancellationToken)
               ?? Array.Empty<ProjectDashboardSummary>();
    }

    public async Task<ProjectDashboardDetails?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/v1/dashboard/projects/{projectId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ProjectDashboardDetails>(
                cancellationToken: cancellationToken);
    }

    public async Task<ProjectClaimDto> CreateClaimAsync(
        Guid projectId,
        CreateClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/projects/{projectId}/claims",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProjectClaimDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Claim API returned an empty response.");
    }
    public async Task<QaRunResultDto?> GetQaRunAsync(Guid runId,CancellationToken cancellationToken=default)
    {
        using var response=await _httpClient.GetAsync($"api/v1/qa/runs/{runId}",cancellationToken);if(response.StatusCode==HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<QaRunResultDto>(cancellationToken:cancellationToken);
    }

    public async Task<IReadOnlyList<QaArtifactDto>> GetQaArtifactsAsync(Guid runId,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<QaArtifactDto>>($"api/v1/qa/runs/{runId}/artifacts",cancellationToken)??Array.Empty<QaArtifactDto>();

    public string QaArtifactUrl(Guid runId,string relativePath)
    {
        var encoded=string.Join('/',relativePath.Split('/',StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return new Uri(_httpClient.BaseAddress??throw new InvalidOperationException("API BaseAddress is missing."),$"api/v1/qa/runs/{runId}/artifacts/{encoded}").ToString();
    }

    public async Task<ForecastResult> GetForecastAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<ForecastResult>(
                   $"api/v1/intelligence/projects/{projectId}/forecast",
                   cancellationToken)
               ?? throw new InvalidDataException("Forecast API returned an empty response.");
    }

    public async Task<PmDecisionBrief> GetDecisionBriefAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<PmDecisionBrief>(
                   $"api/v1/intelligence/projects/{projectId}/brief",
                   cancellationToken)
               ?? throw new InvalidDataException("Decision brief API returned an empty response.");
    }

}
