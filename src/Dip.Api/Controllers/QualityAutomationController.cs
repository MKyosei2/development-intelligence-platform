// Purpose: Exposes Q ua li ty Au to ma ti on Co nt ro ll er HTTP endpoints.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.QualityAutomation.AssetQa;
using Dip.Application.Features.QualityAutomation.VisualQa;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/quality-automation")]
[Produces("application/json")]
public sealed class QualityAutomationController(VisualQaService visual, AssetQaService asset) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("profile")]
    public async Task<ActionResult<QualityAutomationProfileDto>> Profile(Guid projectId, CancellationToken ct)
        => Ok(await visual.GetProfileAsync(projectId, ct));

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPut("profile")]
    public async Task<ActionResult<QualityAutomationProfileDto>> UpdateProfile(
        Guid projectId,
        UpdateQualityAutomationProfileCommand command,
        CancellationToken ct)
        => Ok(await visual.UpdateProfileAsync(projectId, command, ct));

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("goldens")]
    public async Task<ActionResult<IReadOnlyList<GoldenMasterProfileDto>>> Goldens(Guid projectId, CancellationToken ct)
        => Ok(await visual.ListGoldensAsync(projectId, ct));

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("goldens")]
    public async Task<ActionResult<GoldenMasterProfileDto>> RegisterGolden(
        Guid projectId,
        RegisterGoldenMasterCommand command,
        CancellationToken ct)
        => Ok(await visual.RegisterGoldenAsync(projectId, command, ct));

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("visual-comparisons")]
    public async Task<ActionResult<VisualComparisonDto>> Compare(
        Guid projectId,
        RunVisualComparisonCommand command,
        CancellationToken ct)
        => Ok(await visual.CompareAsync(projectId, command, ct));

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("visual-comparisons")]
    public async Task<ActionResult<IReadOnlyList<VisualComparisonDto>>> Comparisons(
        Guid projectId,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
        => Ok(await visual.ListComparisonsAsync(projectId, take, ct));

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("asset-findings")]
    public async Task<ActionResult<IReadOnlyList<AssetQaFindingDto>>> AssetFindings(
        Guid projectId,
        [FromQuery] AssetQaFindingState? state = null,
        [FromQuery] int take = 500,
        CancellationToken ct = default)
        => Ok(await asset.ListAsync(projectId, state, take, ct));
}
