using System.Net;
using System.Net.Http.Json;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.ProjectTruth;

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
}
