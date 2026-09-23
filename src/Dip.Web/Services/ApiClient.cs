// Purpose: Provides the web UI client boundary for DIP project, QA, governance, integration, and management APIs.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.ChangeRisk;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.GitHubIntegration;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.Jobs;
using Dip.Application.Features.Management;
using Dip.Application.Features.Performance;
using Dip.Application.Features.ProjectIntelligence;
using Dip.Application.Features.Projects;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.QualityGovernance;
using Dip.Application.Features.Release;
using Dip.Application.Features.Replay;
using Dip.Application.Features.SystemStatus;
using Dip.Domain.Enums;

namespace Dip.Web.Services;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    internal HttpClient Transport => _httpClient;

    public async Task<IReadOnlyList<ProjectDashboardSummary>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ProjectDashboardSummary>>(
                   "api/v1/dashboard/projects",
                   cancellationToken)
               ?? Array.Empty<ProjectDashboardSummary>();
    }

    public async Task<IReadOnlyList<ProjectDto>> GetProjectRecordsAsync(
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ProjectDto>>(
               "api/projects",
               cancellationToken)
           ?? Array.Empty<ProjectDto>();

    public async Task<ProjectDto?> GetProjectRecordAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/projects/{projectId}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(
            cancellationToken: cancellationToken);
    }

    public async Task<ProjectDto> CreateProjectAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/projects",
            command,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Project API returned an empty response.");
    }

    public async Task<ProjectDto> UpdateProjectAsync(
        Guid projectId,
        UpdateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/projects/{projectId}",
            command,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Project update API returned an empty response.");
    }

    public Task<ProjectDto> ArchiveProjectAsync(
        Guid projectId,
        string reason,
        CancellationToken cancellationToken = default)
        => ChangeProjectLifecycleAsync(projectId, "archive", reason, cancellationToken);

    public Task<ProjectDto> RestoreProjectAsync(
        Guid projectId,
        string reason,
        CancellationToken cancellationToken = default)
        => ChangeProjectLifecycleAsync(projectId, "restore", reason, cancellationToken);

    public Task<ProjectDto> DeleteProjectAsync(
        Guid projectId,
        string reason,
        CancellationToken cancellationToken = default)
        => ChangeProjectLifecycleAsync(projectId, "delete", reason, cancellationToken);

    public async Task<IReadOnlyList<ProjectAuditEntryDto>> GetProjectAuditAsync(
        Guid projectId,
        int take = 200,
        string? search = null,
        string? category = null,
        string? correlationId = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"take={Math.Clamp(take, 1, 5000)}" };
        if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        if (!string.IsNullOrWhiteSpace(category)) query.Add("category=" + Uri.EscapeDataString(category.Trim()));
        if (!string.IsNullOrWhiteSpace(correlationId)) query.Add("correlationId=" + Uri.EscapeDataString(correlationId.Trim()));
        if (fromUtc.HasValue) query.Add("fromUtc=" + Uri.EscapeDataString(fromUtc.Value.ToUniversalTime().ToString("O")));
        if (toUtc.HasValue) query.Add("toUtc=" + Uri.EscapeDataString(toUtc.Value.ToUniversalTime().ToString("O")));
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ProjectAuditEntryDto>>(
                   $"api/projects/{projectId}/audit?{string.Join("&", query)}",
                   cancellationToken)
               ?? Array.Empty<ProjectAuditEntryDto>();
    }

    public async Task<ProjectAuditPageDto> GetProjectAuditPageAsync(
        Guid projectId,
        int take = 100,
        string? search = null,
        string? category = null,
        string? correlationId = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"take={Math.Clamp(take, 1, 500)}" };
        if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        if (!string.IsNullOrWhiteSpace(category)) query.Add("category=" + Uri.EscapeDataString(category.Trim()));
        if (!string.IsNullOrWhiteSpace(correlationId)) query.Add("correlationId=" + Uri.EscapeDataString(correlationId.Trim()));
        if (fromUtc.HasValue) query.Add("fromUtc=" + Uri.EscapeDataString(fromUtc.Value.ToUniversalTime().ToString("O")));
        if (toUtc.HasValue) query.Add("toUtc=" + Uri.EscapeDataString(toUtc.Value.ToUniversalTime().ToString("O")));
        if (!string.IsNullOrWhiteSpace(cursor)) query.Add("cursor=" + Uri.EscapeDataString(cursor));
        return await _httpClient.GetFromJsonAsync<ProjectAuditPageDto>(
                   $"api/projects/{projectId}/audit-page?{string.Join("&", query)}",
                   cancellationToken)
               ?? new ProjectAuditPageDto(Array.Empty<ProjectAuditEntryDto>(), null, false);
    }

    private async Task<ProjectDto> ChangeProjectLifecycleAsync(
        Guid projectId,
        string action,
        string reason,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/projects/{projectId}/{action}",
            new ProjectLifecycleCommand(reason),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Project lifecycle API returned an empty response.");
    }

    public async Task<SystemStatusDto> GetSystemStatusAsync(
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<SystemStatusDto>(
               "api/v1/system/status",
               cancellationToken)
           ?? throw new InvalidDataException("System status API returned an empty response.");

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

        return await response.Content.ReadFromJsonAsync<ProjectDashboardDetails>(
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

    public async Task<QaRunResultDto?> GetQaRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/v1/qa/runs/{runId}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QaRunResultDto>(
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<QaArtifactDto>> GetQaArtifactsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<QaArtifactDto>>(
               $"api/v1/qa/runs/{runId}/artifacts",
               cancellationToken)
           ?? Array.Empty<QaArtifactDto>();

    public async Task<IReadOnlyList<QualityIssueClientDto>> GetQualityIssuesAsync(
        Guid projectId,
        int take = 200,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<QualityIssueClientDto>>(
               $"api/v1/qa/projects/{projectId}/issues?take={Math.Clamp(take, 1, 500)}",
               cancellationToken)
           ?? Array.Empty<QualityIssueClientDto>();

    public async Task<QualityIssueClientDto?> GetQualityIssueAsync(
        Guid projectId,
        Guid issueId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/v1/qa/projects/{projectId}/issues/{issueId}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QualityIssueClientDto>(
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<QaTrendPointClientDto>> GetQaTrendsAsync(
        Guid projectId,
        int take = 30,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<QaTrendPointClientDto>>(
               $"api/v1/qa/projects/{projectId}/trends?take={Math.Clamp(take, 1, 100)}",
               cancellationToken)
           ?? Array.Empty<QaTrendPointClientDto>();

    public string QaArtifactUrl(Guid runId, string relativePath)
    {
        var encoded = string.Join(
            '/',
            relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        return $"/downloads/qa/{runId}/artifacts/{encoded}";
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

    public async Task<IntelligenceDashboardDto> GetIntelligenceDashboardAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IntelligenceDashboardDto>(
               $"api/v1/intelligence-dashboard/projects/{projectId}",
               cancellationToken)
           ?? throw new InvalidDataException("Intelligence dashboard API returned an empty response.");

    public async Task<IReadOnlyList<DurableJobDto>> GetJobsAsync(
        Guid projectId,
        int take = 20,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<DurableJobDto>>(
               $"api/v1/jobs/projects/{projectId}?take={Math.Clamp(take, 1, 200)}",
               cancellationToken)
           ?? Array.Empty<DurableJobDto>();

    public async Task<IReadOnlyList<IncidentSummaryDto>> GetIncidentsAsync(
        Guid projectId,
        int take = 20,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<IncidentSummaryDto>>(
               $"api/v1/incidents/projects/{projectId}?take={Math.Clamp(take, 1, 200)}",
               cancellationToken)
           ?? Array.Empty<IncidentSummaryDto>();

    public async Task<IReadOnlyList<ReplaySessionDto>> GetReplaySessionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ReplaySessionDto>>(
               $"api/v1/replay/projects/{projectId}/sessions",
               cancellationToken)
           ?? Array.Empty<ReplaySessionDto>();

    public async Task<IReadOnlyList<PerformanceBudgetProfileDto>> GetPerformanceBudgetsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<PerformanceBudgetProfileDto>>(
               $"api/v1/performance-budgets?projectId={projectId}",
               cancellationToken)
           ?? Array.Empty<PerformanceBudgetProfileDto>();

    public async Task<ReleaseReadinessDto?> GetReleaseReadinessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/v1/release-readiness/projects/{projectId}/latest",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReleaseReadinessDto>(
            cancellationToken: cancellationToken);
    }

    public async Task<ChangeRiskDto?> GetLatestChangeRiskAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/v1/change-risk/projects/{projectId}/latest",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChangeRiskDto>(
            cancellationToken: cancellationToken);
    }

    public async Task<ManagementDashboardDto> GetManagementDashboardAsync(
        Guid projectId,
        int horizonWeeks = 8,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<ManagementDashboardDto>(
               $"api/v1/project-control/projects/{projectId}/management-dashboard?horizonWeeks={Math.Clamp(horizonWeeks, 1, 26)}",
               cancellationToken)
           ?? throw new InvalidDataException("Management dashboard API returned an empty response.");

    public string ManagementWorkbookUrl(Guid projectId)
        => $"/downloads/projects/{projectId}/management.xlsx";

    public async Task<ProjectControlDashboardDto> GetProjectControlDashboardAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<ProjectControlDashboardDto>(
               $"api/v1/project-control/projects/{projectId}/dashboard",
               cancellationToken)
           ?? throw new InvalidDataException("Project Control dashboard API returned an empty response.");

    public async Task<QaGovernanceOverviewDto> GetQaGovernanceOverviewAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<QaGovernanceOverviewDto>(
               $"api/v1/qa-governance/projects/{projectId}/overview",
               cancellationToken)
           ?? throw new InvalidDataException("QA Governance API returned an empty response.");

    public async Task<PageResult<QaFindingStateDto>> GetQaGovernanceFindingsAsync(
        Guid projectId,
        string? cursor = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<PageResult<QaFindingStateDto>>(
               $"api/v1/qa-governance/projects/{projectId}/findings?pageSize={Math.Clamp(pageSize, 1, 500)}{(string.IsNullOrWhiteSpace(cursor) ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}")}",
               cancellationToken)
           ?? new(Array.Empty<QaFindingStateDto>(), null, pageSize);

    public async Task<IReadOnlyList<QaWaiverDto>> GetQaWaiversAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<QaWaiverDto>>(
               $"api/v1/qa-governance/projects/{projectId}/waivers",
               cancellationToken)
           ?? Array.Empty<QaWaiverDto>();

    public async Task RequestWaiverAsync(
        Guid projectId,
        string name,
        string reason,
        string? fingerprint,
        DateTimeOffset expires,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/qa-governance/waivers",
            new
            {
                projectId,
                name,
                reason,
                fingerprint,
                ruleId = (string?)null,
                pathPattern = (string?)null,
                expiresAtUtc = expires
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task ApproveWaiverAsync(
        Guid projectId,
        Guid id,
        long version,
        string reason,
        CancellationToken ct = default)
        => PostNoContentAsync(
            $"api/v1/qa-governance/projects/{projectId}/waivers/{id}/approve",
            new { reason, expectedVersion = version },
            ct);

    public Task RejectWaiverAsync(
        Guid projectId,
        Guid id,
        long version,
        string reason,
        CancellationToken ct = default)
        => PostNoContentAsync(
            $"api/v1/qa-governance/projects/{projectId}/waivers/{id}/reject",
            new { reason, expectedVersion = version },
            ct);

    public async Task<IReadOnlyList<QaPolicyDto>> GetQaPoliciesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<QaPolicyDto>>(
               $"api/v1/qa-governance/projects/{projectId}/policies",
               cancellationToken)
           ?? Array.Empty<QaPolicyDto>();

    public async Task CreateQaPolicyAsync(
        Guid projectId,
        string name,
        string version,
        QaPolicyRules rules,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/qa-governance/policies",
            new { projectId, name, version, rules },
            ct);
        response.EnsureSuccessStatusCode();
    }

    public Task ApproveQaPolicyAsync(Guid projectId, Guid id, long version, string reason, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/policies/{id}/approve", new { reason, expectedVersion = version }, ct);

    public Task ActivateQaPolicyAsync(Guid projectId, Guid id, long version, string reason, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/policies/{id}/activate", new { reason, expectedVersion = version }, ct);

    public async Task<IReadOnlyList<QaReleaseGateOverrideDto>> GetQaReleaseOverridesAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<QaReleaseGateOverrideDto>>($"api/v1/qa-governance/projects/{projectId}/release-overrides", ct)
           ?? Array.Empty<QaReleaseGateOverrideDto>();

    public async Task RequestQaReleaseOverrideAsync(Guid projectId, string reason, DateTimeOffset expires, CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/qa-governance/release-overrides",
            new
            {
                projectId,
                policyEvaluationId = (Guid?)null,
                qaRunId = (Guid?)null,
                commitSha = (string?)null,
                reason,
                expiresAtUtc = expires
            },
            ct);
        response.EnsureSuccessStatusCode();
    }

    public Task ApproveQaReleaseOverrideAsync(Guid projectId, Guid id, long version, string reason, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/qa-governance/projects/{projectId}/release-overrides/{id}/approve", new { reason, expectedVersion = version }, ct);

    public async Task<IReadOnlyList<GovernanceAuditLogDto>> GetQaAuditAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<GovernanceAuditLogDto>>($"api/v1/qa-governance/projects/{projectId}/audit?take=200", ct)
           ?? Array.Empty<GovernanceAuditLogDto>();

    public async Task<IReadOnlyList<QaBackupVerificationDto>> GetQaBackupVerificationsAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<QaBackupVerificationDto>>($"api/v1/qa-governance/projects/{projectId}/backup-verifications?take=100", ct)
           ?? Array.Empty<QaBackupVerificationDto>();

    public async Task<IReadOnlyList<ProjectMembershipDto>> GetProjectMembershipsAsync(
        Guid projectId,
        CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ProjectMembershipDto>>(
               $"api/v1/projects/{projectId}/memberships",
               ApiJsonOptions,
               ct)
           ?? Array.Empty<ProjectMembershipDto>();

    public async Task UpsertProjectMembershipAsync(
        Guid projectId,
        string subjectId,
        string login,
        ProjectMembershipRole role,
        string reason,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/v1/projects/{projectId}/memberships/{Uri.EscapeDataString(subjectId)}",
            new { subjectId, login, role, reason },
            ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
            throw new InvalidOperationException(
                error is not null && error.TryGetValue("message", out var message)
                    ? message
                    : "Project ownership constraints rejected the membership change.");
        }
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveProjectMembershipAsync(
        Guid projectId,
        string subjectId,
        string reason,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"api/v1/projects/{projectId}/memberships/{Uri.EscapeDataString(subjectId)}")
        {
            Content = JsonContent.Create(new { reason })
        };
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task TransferProjectOwnershipAsync(
        Guid projectId,
        string newOwnerSubjectId,
        string newOwnerLogin,
        string reason,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/projects/{projectId}/memberships/transfer-owner",
            new { newOwnerSubjectId, newOwnerLogin, reason },
            ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<PipelineRunClientDto>> GetPipelineRunsAsync(
        Guid projectId,
        int take = 100,
        CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<PipelineRunClientDto>>(
               $"api/v1/pipeline-runs/projects/{projectId}?take={Math.Clamp(take, 1, 500)}",
               ct)
           ?? Array.Empty<PipelineRunClientDto>();

    public async Task<PipelineRunClientDto?> GetPipelineRunAsync(
        Guid runId,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync($"api/v1/pipeline-runs/{runId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PipelineRunClientDto>(cancellationToken: ct);
    }

    public async Task<GitHubConnectionStatusDto> GetGitHubConnectionStatusAsync(
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<GitHubConnectionStatusDto>(
               "api/v1/integrations/github/status",
               cancellationToken)
           ?? throw new InvalidDataException("GitHub connection API returned an empty response.");

    public async Task<GitHubConnectionTestClientDto> TestGitHubConnectionAsync(
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var path = projectId.HasValue
            ? $"api/v1/integrations/github/projects/{projectId.Value}/test"
            : "api/v1/integrations/github/test";
        using var response = await _httpClient.PostAsync(
            path,
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubConnectionTestClientDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("GitHub test API returned an empty response.");
    }

    public async Task<IReadOnlyList<GitHubRepositorySummaryDto>> GetGitHubRepositoriesAsync(
        string? search = null,
        string? visibility = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            query.Add("search=" + Uri.EscapeDataString(search));
        if (!string.IsNullOrWhiteSpace(visibility))
            query.Add("visibility=" + Uri.EscapeDataString(visibility));
        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<GitHubRepositorySummaryDto>>(
                   "api/v1/integrations/github/repositories" + suffix,
                   cancellationToken)
               ?? Array.Empty<GitHubRepositorySummaryDto>();
    }

    public async Task<ImportGitHubProjectResult> ImportGitHubProjectAsync(
        ImportGitHubProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/integrations/github/import",
            request,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("This repository is already registered as a DIP project.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportGitHubProjectResult>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("GitHub import API returned an empty response.");
    }

    public async Task<GitHubProjectRepositoryDto> GetProjectRepositoryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<GitHubProjectRepositoryDto>(
               $"api/v1/integrations/github/projects/{projectId}/repository",
               cancellationToken)
           ?? throw new InvalidDataException("Project repository API returned an empty response.");

    public async Task<IReadOnlyList<PullRequestSummaryClientDto>> GetPullRequestsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<PullRequestSummaryClientDto>>(
               $"api/v1/integrations/github/projects/{projectId}/pull-requests",
               cancellationToken)
           ?? Array.Empty<PullRequestSummaryClientDto>();

    public async Task<PullRequestDetailsClientDto?> GetPullRequestAsync(
        Guid projectId,
        int number,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/v1/integrations/github/projects/{projectId}/pull-requests/{number}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PullRequestDetailsClientDto>(
            cancellationToken: cancellationToken);
    }

    public async Task<GitHubWorkspaceSyncDto> SyncProjectRepositoryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            $"api/v1/integrations/github/projects/{projectId}/sync",
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubWorkspaceSyncDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("Repository sync API returned an empty response.");
    }

    public async Task<QaRunResultDto> RunQaAsync(
        QaExecutionCommand command,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/qa/runs",
            command,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QaRunResultDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("QA run API returned an empty response.");
    }

    public async Task<DailyCockpitDto> GetDailyCockpitAsync(
        Guid projectId,
        int horizonWeeks = 8,
        string? ownerRole = null,
        CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<DailyCockpitDto>(
               $"api/v1/management/projects/{projectId}/cockpit?horizonWeeks={Math.Clamp(horizonWeeks, 1, 26)}{(string.IsNullOrWhiteSpace(ownerRole) ? string.Empty : $"&ownerRole={Uri.EscapeDataString(ownerRole)}")}",
               ct)
           ?? throw new InvalidDataException("Management cockpit API returned an empty response.");

    public async Task<RaciOverviewDto> GetRaciAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<RaciOverviewDto>($"api/v1/management/projects/{projectId}/raci", ct)
           ?? throw new InvalidDataException("RACI API returned an empty response.");

    public async Task<ManagementRoleDirectoryDto> GetManagementRolesAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<ManagementRoleDirectoryDto>($"api/v1/management/projects/{projectId}/management-roles", ct)
           ?? throw new InvalidDataException("Management role directory API returned an empty response.");

    public async Task<WorkCalendarOverviewDto> GetWorkCalendarsAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<WorkCalendarOverviewDto>($"api/v1/management/projects/{projectId}/calendars", ct)
           ?? throw new InvalidDataException("Calendar API returned an empty response.");

    public async Task<ApprovalOverviewDto> GetApprovalsAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<ApprovalOverviewDto>($"api/v1/management/projects/{projectId}/approvals", ct)
           ?? throw new InvalidDataException("Approval API returned an empty response.");

    public async Task<IReadOnlyList<OperationalAlertDto>> GetOperationalAlertsAsync(
        Guid projectId,
        int horizonWeeks = 8,
        CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<OperationalAlertDto>>(
               $"api/v1/management/projects/{projectId}/alerts?horizonWeeks={Math.Clamp(horizonWeeks, 1, 26)}",
               ct)
           ?? Array.Empty<OperationalAlertDto>();

    public async Task<IReadOnlyList<NotificationHistoryEntryDto>> GetAlertHistoryAsync(
        Guid projectId,
        string alertKey,
        int take = 100,
        CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<NotificationHistoryEntryDto>>(
               $"api/v1/management/projects/{projectId}/alerts/{Uri.EscapeDataString(alertKey)}/history?take={Math.Clamp(take, 1, 200)}",
               ct)
           ?? Array.Empty<NotificationHistoryEntryDto>();

    public async Task<OperatingCadenceDto> GetOperatingCadenceAsync(
        Guid projectId,
        string mode,
        int horizonWeeks = 8,
        CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<OperatingCadenceDto>(
               $"api/v1/management/projects/{projectId}/cadence/{Uri.EscapeDataString(mode)}?horizonWeeks={Math.Clamp(horizonWeeks, 1, 26)}",
               ct)
           ?? throw new InvalidDataException("Cadence API returned an empty response.");

    public async Task<SyncStateOverviewDto> GetManagementSyncStateAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<SyncStateOverviewDto>($"api/v1/management/projects/{projectId}/sync-state", ct)
           ?? throw new InvalidDataException("Management sync state API returned an empty response.");

    public async Task<PortfolioManagementDto> GetPortfolioManagementAsync(int horizonWeeks = 8, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<PortfolioManagementDto>($"api/v1/management/portfolio?horizonWeeks={Math.Clamp(horizonWeeks, 1, 26)}", ct)
           ?? throw new InvalidDataException("Portfolio management API returned an empty response.");

    public async Task<GoogleSheetsSyncResult> SyncManagementGoogleSheetsAsync(GoogleSheetsSyncCommand command, CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/v1/project-control/google-sheets/sync", command, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GoogleSheetsSyncResult>(cancellationToken: ct)
               ?? throw new InvalidDataException("Google Sheets sync API returned an empty response.");
    }

    public async Task<GoogleCalendarSyncResult> SyncManagementGoogleCalendarAsync(
        Guid projectId,
        GoogleCalendarSyncCommand command,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/v1/management/projects/{projectId}/calendars/google-sync", command, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GoogleCalendarSyncResult>(cancellationToken: ct)
               ?? throw new InvalidDataException("Google Calendar sync API returned an empty response.");
    }

    public Task UpsertRiskAsync(UpsertRiskCommand command, CancellationToken ct = default)
        => PostNoContentAsync("api/v1/project-control/risks", command, ct);

    public Task UpsertManagementDecisionAsync(UpsertManagementDecisionCommand command, CancellationToken ct = default)
        => PostNoContentAsync("api/v1/project-control/management-decisions", command, ct);

    public Task UpsertManagementActionAsync(UpsertActionItemCommand command, CancellationToken ct = default)
        => PostNoContentAsync("api/v1/project-control/management-actions", command, ct);

    public Task UpsertResourceAllocationAsync(UpsertResourceAllocationCommand command, CancellationToken ct = default)
        => PostNoContentAsync("api/v1/project-control/resource-allocations", command, ct);

    public Task UpsertChangeRequestAsync(UpsertChangeRequestCommand command, CancellationToken ct = default)
        => PostNoContentAsync("api/v1/project-control/change-requests", command, ct);

    public Task UpsertRaciAsync(Guid projectId, UpsertResponsibilityCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/management/projects/{projectId}/raci", command, ct);

    public Task UpsertManagementRoleAsync(Guid projectId, UpsertManagementRoleBindingCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/management/projects/{projectId}/management-roles", command, ct);

    public Task UpsertSharedResourceMembershipAsync(Guid projectId, UpsertSharedResourceMembershipCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/management/projects/{projectId}/shared-resource-membership", command, ct);

    public Task UpsertWorkCalendarAsync(Guid projectId, UpsertWorkCalendarCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/management/projects/{projectId}/calendars", command, ct);

    public Task CreateApprovalAsync(Guid projectId, CreateApprovalRequestCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/management/projects/{projectId}/approvals", command, ct);

    public async Task<ApprovalRequestDto> RespondApprovalAsync(
        Guid projectId,
        string key,
        ApprovalResponseCommand command,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/management/projects/{projectId}/approvals/{Uri.EscapeDataString(key)}/respond",
            command,
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApprovalRequestDto>(cancellationToken: ct)
               ?? throw new InvalidDataException("Approval response API returned an empty response.");
    }

    public async Task<AlertStateDto> SetAlertDispositionAsync(
        Guid projectId,
        string alertKey,
        AlertDispositionCommand command,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/management/projects/{projectId}/alerts/{Uri.EscapeDataString(alertKey)}/disposition",
            command,
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AlertStateDto>(cancellationToken: ct)
               ?? throw new InvalidDataException("Alert disposition API returned an empty response.");
    }

    public Task UpsertSourceAuthorityAsync(Guid projectId, SourceAuthorityRuleCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/management/projects/{projectId}/source-authorities", command, ct);

    public async Task<ManagementSyncResult> RunManagementSyncAsync(
        Guid projectId,
        ManagementSyncCommand command,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/v1/management/projects/{projectId}/sync", command, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ManagementSyncResult>(cancellationToken: ct)
               ?? throw new InvalidDataException("Management sync API returned an empty response.");
    }

    public async Task<SyncConflictDto> ResolveManagementSyncConflictAsync(
        Guid projectId,
        string conflictKey,
        ResolveSyncConflictCommand command,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/management/projects/{projectId}/sync-conflicts/{Uri.EscapeDataString(conflictKey)}/resolve",
            command,
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SyncConflictDto>(cancellationToken: ct)
               ?? throw new InvalidDataException("Sync conflict API returned an empty response.");
    }

    public Task ArchiveManagementItemAsync(Guid projectId, ArchiveManagementItemCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/management/projects/{projectId}/archive", command, ct);

    public async Task<ProjectIntelligenceLayerDto> GetProjectIntelligenceLayerAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<ProjectIntelligenceLayerDto>($"api/v1/project-intelligence/projects/{projectId}/overview", ct)
           ?? throw new InvalidDataException("Project intelligence API returned an empty response.");

    public async Task<IReadOnlyList<ProjectSourceProviderDto>> GetProjectSourceProvidersAsync(Guid projectId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ProjectSourceProviderDto>>($"api/v1/project-intelligence/projects/{projectId}/sources", ct)
           ?? Array.Empty<ProjectSourceProviderDto>();

    public async Task<ProjectSourceSyncResult> SyncProjectSourceAsync(
        Guid projectId,
        string provider,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsync(
            $"api/v1/project-intelligence/projects/{projectId}/sources/{Uri.EscapeDataString(provider)}/sync?dryRun={dryRun.ToString().ToLowerInvariant()}",
            null,
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectSourceSyncResult>(cancellationToken: ct)
               ?? throw new InvalidDataException("Project source sync API returned an empty response.");
    }

    public Task<ProjectPlanAnalysisDto> AnalyzeProjectPlanAsync(
        Guid projectId,
        string fileName,
        string? contentType,
        Stream stream,
        CancellationToken ct = default)
        => UploadPlanAsync<ProjectPlanAnalysisDto>(
            $"api/v1/project-intelligence/projects/{projectId}/plans/analyze",
            fileName,
            contentType,
            stream,
            null,
            ct);

    public Task<ProjectPlanImportResult> ImportProjectPlanAsync(
        Guid projectId,
        string fileName,
        string? contentType,
        Stream stream,
        IReadOnlyList<PlanColumnMappingDto>? mappings,
        CancellationToken ct = default)
        => UploadPlanAsync<ProjectPlanImportResult>(
            $"api/v1/project-intelligence/projects/{projectId}/plans/import",
            fileName,
            contentType,
            stream,
            mappings,
            ct);

    public Task BindProjectEntityAsync(Guid projectId, BindProjectEntityCommand command, CancellationToken ct = default)
        => PostNoContentAsync($"api/v1/project-intelligence/projects/{projectId}/entity-bindings", command, ct);

    private async Task<T> UploadPlanAsync<T>(
        string uri,
        string fileName,
        string? contentType,
        Stream stream,
        IReadOnlyList<PlanColumnMappingDto>? mappings,
        CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(stream);
        if (!string.IsNullOrWhiteSpace(contentType))
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        if (mappings is not null)
            form.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(mappings)), "mappingJson");
        using var response = await _httpClient.PostAsync(uri, form, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
               ?? throw new InvalidDataException($"{typeof(T).Name} API returned an empty response.");
    }

    private async Task PostNoContentAsync(string uri, object payload, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync(uri, payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
