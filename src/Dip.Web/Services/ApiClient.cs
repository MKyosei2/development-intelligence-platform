using System.Net;
using System.Net.Http.Json;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Replay;
using Dip.Application.Features.Release;
using Dip.Application.Features.Performance;
using Dip.Application.Features.Jobs;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.ChangeRisk;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.Management;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.QualityGovernance;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;

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

    public async Task<IntelligenceDashboardDto> GetIntelligenceDashboardAsync(Guid projectId,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<IntelligenceDashboardDto>($"api/v1/intelligence-dashboard/projects/{projectId}",cancellationToken)??throw new InvalidDataException("Intelligence dashboard API returned an empty response.");

    public async Task<IReadOnlyList<DurableJobDto>> GetJobsAsync(Guid projectId,int take=20,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<DurableJobDto>>($"api/v1/jobs/projects/{projectId}?take={Math.Clamp(take,1,200)}",cancellationToken)??Array.Empty<DurableJobDto>();

    public async Task<IReadOnlyList<IncidentSummaryDto>> GetIncidentsAsync(Guid projectId,int take=20,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<IncidentSummaryDto>>($"api/v1/incidents/projects/{projectId}?take={Math.Clamp(take,1,200)}",cancellationToken)??Array.Empty<IncidentSummaryDto>();

    public async Task<IReadOnlyList<ReplaySessionDto>> GetReplaySessionsAsync(Guid projectId,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<ReplaySessionDto>>($"api/v1/replay/projects/{projectId}/sessions",cancellationToken)??Array.Empty<ReplaySessionDto>();

    public async Task<IReadOnlyList<PerformanceBudgetProfileDto>> GetPerformanceBudgetsAsync(Guid projectId,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<IReadOnlyList<PerformanceBudgetProfileDto>>($"api/v1/performance-budgets?projectId={projectId}",cancellationToken)??Array.Empty<PerformanceBudgetProfileDto>();

    public async Task<ReleaseReadinessDto?> GetReleaseReadinessAsync(Guid projectId,CancellationToken cancellationToken=default)
    {using var response=await _httpClient.GetAsync($"api/v1/release-readiness/projects/{projectId}/latest",cancellationToken);if(response.StatusCode==HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<ReleaseReadinessDto>(cancellationToken:cancellationToken);}

    public async Task<ChangeRiskDto?> GetLatestChangeRiskAsync(Guid projectId,CancellationToken cancellationToken=default)
    {using var response=await _httpClient.GetAsync($"api/v1/change-risk/projects/{projectId}/latest",cancellationToken);if(response.StatusCode==HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<ChangeRiskDto>(cancellationToken:cancellationToken);}

    public async Task<ManagementDashboardDto> GetManagementDashboardAsync(Guid projectId,int horizonWeeks=8,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<ManagementDashboardDto>($"api/v1/project-control/projects/{projectId}/management-dashboard?horizonWeeks={Math.Clamp(horizonWeeks,1,26)}",cancellationToken)
          ??throw new InvalidDataException("Management dashboard API returned an empty response.");

    public string ManagementWorkbookUrl(Guid projectId)
        =>new Uri(_httpClient.BaseAddress??throw new InvalidOperationException("API BaseAddress is missing."),$"api/v1/project-control/projects/{projectId}/management.xlsx").ToString();

    public async Task<ProjectControlDashboardDto> GetProjectControlDashboardAsync(Guid projectId,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<ProjectControlDashboardDto>($"api/v1/project-control/projects/{projectId}/dashboard",cancellationToken)
          ??throw new InvalidDataException("Project Control dashboard API returned an empty response.");

    public async Task<QaGovernanceOverviewDto> GetQaGovernanceOverviewAsync(Guid projectId,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<QaGovernanceOverviewDto>($"api/v1/qa-governance/projects/{projectId}/overview",cancellationToken)
          ??throw new InvalidDataException("QA Governance API returned an empty response.");


    public async Task<PageResult<QaFindingStateDto>> GetQaGovernanceFindingsAsync(Guid projectId,string? cursor=null,int pageSize=100,CancellationToken cancellationToken=default)
        =>await _httpClient.GetFromJsonAsync<PageResult<QaFindingStateDto>>($"api/v1/qa-governance/projects/{projectId}/findings?pageSize={Math.Clamp(pageSize,1,500)}{(string.IsNullOrWhiteSpace(cursor)?string.Empty:$"&cursor={Uri.EscapeDataString(cursor)}")}",cancellationToken)??new(Array.Empty<QaFindingStateDto>(),null,pageSize);
    public async Task<IReadOnlyList<QaWaiverDto>> GetQaWaiversAsync(Guid projectId,CancellationToken cancellationToken=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<QaWaiverDto>>($"api/v1/qa-governance/projects/{projectId}/waivers",cancellationToken)??Array.Empty<QaWaiverDto>();
    public async Task RequestWaiverAsync(Guid projectId,string name,string reason,string? fingerprint,DateTimeOffset expires,CancellationToken cancellationToken=default){using var r=await _httpClient.PostAsJsonAsync("api/v1/qa-governance/waivers",new{projectId,name,reason,fingerprint,ruleId=(string?)null,pathPattern=(string?)null,expiresAtUtc=expires},cancellationToken);r.EnsureSuccessStatusCode();}
    public Task ApproveWaiverAsync(Guid projectId,Guid id,long version,string reason,CancellationToken ct=default)=>PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/waivers/{id}/approve",new{reason,expectedVersion=version},ct);
    public Task RejectWaiverAsync(Guid projectId,Guid id,long version,string reason,CancellationToken ct=default)=>PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/waivers/{id}/reject",new{reason,expectedVersion=version},ct);
    public async Task<IReadOnlyList<QaPolicyDto>> GetQaPoliciesAsync(Guid projectId,CancellationToken cancellationToken=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<QaPolicyDto>>($"api/v1/qa-governance/projects/{projectId}/policies",cancellationToken)??Array.Empty<QaPolicyDto>();
    public async Task CreateQaPolicyAsync(Guid projectId,string name,string version,QaPolicyRules rules,CancellationToken ct=default){using var r=await _httpClient.PostAsJsonAsync("api/v1/qa-governance/policies",new{projectId,name,version,rules},ct);r.EnsureSuccessStatusCode();}
    public Task ApproveQaPolicyAsync(Guid projectId,Guid id,long version,string reason,CancellationToken ct=default)=>PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/policies/{id}/approve",new{reason,expectedVersion=version},ct);
    public Task ActivateQaPolicyAsync(Guid projectId,Guid id,long version,string reason,CancellationToken ct=default)=>PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/policies/{id}/activate",new{reason,expectedVersion=version},ct);
    public async Task<IReadOnlyList<QaReleaseGateOverrideDto>> GetQaReleaseOverridesAsync(Guid projectId,CancellationToken ct=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<QaReleaseGateOverrideDto>>($"api/v1/qa-governance/projects/{projectId}/release-overrides",ct)??Array.Empty<QaReleaseGateOverrideDto>();
    public async Task RequestQaReleaseOverrideAsync(Guid projectId,string reason,DateTimeOffset expires,CancellationToken ct=default){using var r=await _httpClient.PostAsJsonAsync("api/v1/qa-governance/release-overrides",new{projectId,policyEvaluationId=(Guid?)null,qaRunId=(Guid?)null,commitSha=(string?)null,reason,expiresAtUtc=expires},ct);r.EnsureSuccessStatusCode();}
    public Task ApproveQaReleaseOverrideAsync(Guid projectId,Guid id,long version,string reason,CancellationToken ct=default)=>PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/release-overrides/{id}/approve",new{reason,expectedVersion=version},ct);
    public async Task<IReadOnlyList<GovernanceAuditLogDto>> GetQaAuditAsync(Guid projectId,CancellationToken ct=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<GovernanceAuditLogDto>>($"api/v1/qa-governance/projects/{projectId}/audit?take=200",ct)??Array.Empty<GovernanceAuditLogDto>();
    public async Task<IReadOnlyList<QaBackupVerificationDto>> GetQaBackupVerificationsAsync(Guid projectId,CancellationToken ct=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<QaBackupVerificationDto>>($"api/v1/qa-governance/projects/{projectId}/backup-verifications?take=100",ct)??Array.Empty<QaBackupVerificationDto>();
    public async Task<IReadOnlyList<ProjectMembershipDto>> GetProjectMembershipsAsync(Guid projectId,CancellationToken ct=default)=>await _httpClient.GetFromJsonAsync<IReadOnlyList<ProjectMembershipDto>>($"api/v1/projects/{projectId}/memberships",ct)??Array.Empty<ProjectMembershipDto>();
    public async Task UpsertProjectMembershipAsync(Guid projectId,string subjectId,string login,ProjectMembershipRole role,string reason,CancellationToken ct=default){using var r=await _httpClient.PutAsJsonAsync($"api/v1/projects/{projectId}/memberships/{Uri.EscapeDataString(subjectId)}",new{subjectId,login,role,reason},ct);r.EnsureSuccessStatusCode();}
    private async Task PostNoContentAsync(string uri,object payload,CancellationToken ct){using var r=await _httpClient.PostAsJsonAsync(uri,payload,ct);r.EnsureSuccessStatusCode();}

}
