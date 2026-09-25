// Purpose: Exposes Notifications HTTP endpoints for the DIP API.
using Dip.Api.Authorization;
using Dip.Application.Features.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

/// <summary>
/// Management escalation feed. It deliberately reports project/workstream conditions rather
/// than individual productivity. Consumers can poll this endpoint or use the spreadsheet sync
/// output to surface critical/high management notifications.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(ManagementAnalyticsService management) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/management")]
    public Task<EscalationOverviewDto> GetManagementNotifications(Guid projectId, [FromQuery] int horizonWeeks = 8, CancellationToken ct = default)
        => management.GetAlertsAsync(projectId, horizonWeeks, ct);
}
