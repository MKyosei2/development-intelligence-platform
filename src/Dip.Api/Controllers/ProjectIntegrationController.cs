// Purpose: Exposes version-control and CI synchronization endpoints for project control.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.Assets;
using Dip.Application.Features.ChangeImpact;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Decisions;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.FeatureControl;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Management;
using Dip.Application.Features.Milestones;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.Review;
using Dip.Application.Features.Scenarios;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectControlDecisionBriefDto = Dip.Application.Features.Decisions.DecisionBriefDto;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-control")]
public sealed class ProjectIntegrationController(VersionControlSyncService versionControl, ExternalCiIntegrationService ci) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("version-control/sync")]
    public Task<NormalizedVersionControlChange> SyncVersionControl([FromBody] VersionControlSyncRequest request, CancellationToken ct)
            => versionControl.SyncAsync(request, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("ci/sync")]
    public Task<NormalizedCiRun> SyncCi([FromBody] ExternalCiSyncRequest request, CancellationToken ct)
            => ci.SyncAsync(request, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("ci/ingest")]
    public Task<NormalizedCiRun> IngestCi([FromBody] NormalizedCiRun run, CancellationToken ct)
            => ci.IngestAsync(run, ct);
}
