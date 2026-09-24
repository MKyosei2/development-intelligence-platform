// Purpose: Exposes M an ag em en tO pe ra ti on s HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Management;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/management")]
public sealed class ManagementOperationsController(ManagementOperationsService operations, ManagementSyncService sync, WorkCalendarIntegrationService calendarSync) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/cockpit")]
    public Task<DailyCockpitDto> Cockpit(Guid projectId, [FromQuery] int horizonWeeks = 8, [FromQuery] string? ownerRole = null, CancellationToken ct = default)
        => operations.GetDailyCockpitAsync(projectId, horizonWeeks, ownerRole, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/raci")]
    public Task<RaciOverviewDto> Raci(Guid projectId, CancellationToken ct) => operations.GetRaciAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("projects/{projectId:guid}/raci")]
    public Task<Dip.Application.Features.DevelopmentGraph.DevelopmentGraphNodeDto> UpsertRaci(Guid projectId, [FromBody] UpsertResponsibilityCommand command, CancellationToken ct)
        => operations.UpsertResponsibilityAsync(command with { ProjectId = projectId }, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/management-roles")]
    public Task<ManagementRoleDirectoryDto> ManagementRoles(Guid projectId, CancellationToken ct)
        => operations.GetManagementRoleDirectoryAsync(projectId, RequestActor.AuditIdentity(User), ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "projectId")]
    [HttpPost("projects/{projectId:guid}/management-roles")]
    public Task<Dip.Application.Features.DevelopmentGraph.DevelopmentGraphNodeDto> UpsertManagementRole(Guid projectId, [FromBody] UpsertManagementRoleBindingCommand command, CancellationToken ct)
        => operations.UpsertManagementRoleBindingAsync(command with { ProjectId = projectId }, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/shared-resource-membership")]
    public Task<Dip.Application.Features.DevelopmentGraph.DevelopmentGraphNodeDto> UpsertSharedResourceMembership(Guid projectId, [FromBody] UpsertSharedResourceMembershipCommand command, CancellationToken ct)
        => operations.UpsertSharedResourceMembershipAsync(command with { ProjectId = projectId }, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/calendars")]
    public Task<WorkCalendarOverviewDto> Calendars(Guid projectId, CancellationToken ct) => operations.GetCalendarsAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/calendars/google-sync")]
    public Task<GoogleCalendarSyncResult> SyncGoogleCalendar(Guid projectId, [FromBody] GoogleCalendarSyncCommand command, CancellationToken ct)
        => calendarSync.SyncGoogleAsync(command with { ProjectId = projectId }, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/calendars")]
    public Task<Dip.Application.Features.DevelopmentGraph.DevelopmentGraphNodeDto> UpsertCalendar(Guid projectId, [FromBody] UpsertWorkCalendarCommand command, CancellationToken ct)
        => operations.UpsertWorkCalendarAsync(command with { ProjectId = projectId }, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/approvals")]
    public Task<ApprovalOverviewDto> Approvals(Guid projectId, CancellationToken ct) => operations.GetApprovalsAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/approvals")]
    public Task<Dip.Application.Features.DevelopmentGraph.DevelopmentGraphNodeDto> CreateApproval(Guid projectId, [FromBody] CreateApprovalRequestCommand command, CancellationToken ct)
        => operations.CreateApprovalAsync(command with { ProjectId = projectId, RequestedBy = RequestActor.AuditIdentity(User) }, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/approvals/{key}/respond")]
    public Task<ApprovalRequestDto> RespondApproval(Guid projectId, string key, [FromBody] ApprovalResponseCommand command, CancellationToken ct)
        => operations.RespondApprovalAsync(command with { ProjectId = projectId, Key = key, Actor = RequestActor.AuditIdentity(User) }, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/alerts")]
    public Task<IReadOnlyList<OperationalAlertDto>> Alerts(Guid projectId, [FromQuery] int horizonWeeks = 8, CancellationToken ct = default)
        => operations.GetOperationalAlertsAsync(projectId, horizonWeeks, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/alerts/{alertKey}/history")]
    public Task<IReadOnlyList<NotificationHistoryEntryDto>> AlertHistory(Guid projectId, string alertKey, [FromQuery] int take = 100, CancellationToken ct = default)
        => operations.GetAlertHistoryAsync(projectId, alertKey, take, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("projects/{projectId:guid}/alerts/{alertKey}/disposition")]
    public Task<AlertStateDto> SetAlertDisposition(Guid projectId, string alertKey, [FromBody] AlertDispositionCommand command, CancellationToken ct)
        => operations.SetAlertDispositionAsync(command with { ProjectId = projectId, AlertKey = alertKey, Actor = RequestActor.AuditIdentity(User) }, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/cadence/{mode}")]
    public Task<OperatingCadenceDto> Cadence(Guid projectId, string mode, [FromQuery] int horizonWeeks = 8, CancellationToken ct = default)
        => operations.GetCadenceAsync(projectId, mode, horizonWeeks, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "projectId")]
    [HttpPost("projects/{projectId:guid}/source-authorities")]
    public Task<Dip.Application.Features.DevelopmentGraph.DevelopmentGraphNodeDto> UpsertAuthority(Guid projectId, [FromBody] SourceAuthorityRuleCommand command, CancellationToken ct)
        => operations.UpsertSourceAuthorityAsync(command with { ProjectId = projectId }, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/sync-state")]
    public Task<SyncStateOverviewDto> SyncState(Guid projectId, CancellationToken ct) => operations.GetSyncStateAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/sync-conflicts/{conflictKey}/resolve")]
    public Task<SyncConflictDto> ResolveConflict(Guid projectId, string conflictKey, [FromBody] ResolveSyncConflictCommand command, CancellationToken ct)
        => operations.ResolveConflictAsync(command with { ProjectId = projectId, ConflictKey = conflictKey, ResolvedBy = RequestActor.AuditIdentity(User) }, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/sync")]
    public Task<ManagementSyncResult> Synchronize(Guid projectId, [FromBody] ManagementSyncCommand command, CancellationToken ct)
        => sync.SyncAsync(command with { ProjectId = projectId }, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPost("projects/{projectId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid projectId, [FromBody] ArchiveManagementItemCommand command, CancellationToken ct)
    { await operations.ArchiveAsync(command with { ProjectId = projectId, Actor = RequestActor.AuditIdentity(User) }, ct); return NoContent(); }
}
