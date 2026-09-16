// Purpose: Exposes P ro je ct In te ll ig en ce So ur ce sC on tr ol le r HTTP endpoints.
using Dip.Api.Authorization;
using Dip.Application.Features.ProjectIntelligence;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-intelligence")]
public sealed class ProjectIntelligenceSourcesController(
    ProjectPlanIngestionService plans,ProjectSourceIngestionService sources,ProjectProgressIntelligenceService intelligence,ProjectEntityResolutionService resolution):ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer,"projectId")]
    [HttpGet("projects/{projectId:guid}/overview")]
    public Task<ProjectIntelligenceLayerDto> Overview(Guid projectId,CancellationToken ct)=>intelligence.GetLayerAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer,"projectId")]
    [HttpGet("projects/{projectId:guid}/gantt")]
    public Task<WbsGanttIntelligenceDto> Gantt(Guid projectId,CancellationToken ct)=>intelligence.GetGanttAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer,"projectId")]
    [HttpGet("projects/{projectId:guid}/digest")]
    public Task<ProjectManagementDigestDto> Digest(Guid projectId,CancellationToken ct)=>intelligence.GetDigestAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer,"projectId")]
    [HttpGet("projects/{projectId:guid}/entity-resolution")]
    public Task<EntityResolutionOverviewDto> EntityResolution(Guid projectId,CancellationToken ct)=>resolution.ResolveProjectAsync(projectId,false,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer,"projectId")]
    [HttpGet("projects/{projectId:guid}/sources")]
    public ActionResult<IReadOnlyList<ProjectSourceProviderDto>> Providers(Guid projectId)=>Ok(sources.Providers(projectId).Select(x=>new ProjectSourceProviderDto(x.Provider,x.Configured)).ToArray());

    [Authorize(Policy=Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer,"projectId")]
    [HttpPost("projects/{projectId:guid}/sources/{provider}/sync")]
    public Task<ProjectSourceSyncResult> Sync(Guid projectId,string provider,[FromQuery]bool dryRun=false,CancellationToken ct=default)
        =>sources.SynchronizeAsync(new(projectId,provider,dryRun),ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer,"projectId")]
    [RequestSizeLimit(ProjectPlanIngestionService.MaxUploadBytes+1024*1024)]
    [HttpPost("projects/{projectId:guid}/plans/analyze")]
    public async Task<ProjectPlanAnalysisDto> AnalyzePlan(Guid projectId,IFormFile file,CancellationToken ct)
    {if(file is null||file.Length<=0)throw new ArgumentException("Plan file is required.",nameof(file));await using var stream=file.OpenReadStream();return await plans.AnalyzeAsync(file.FileName,stream,ct);}

    [Authorize(Policy=Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer,"projectId")]
    [RequestSizeLimit(ProjectPlanIngestionService.MaxUploadBytes+1024*1024)]
    [HttpPost("projects/{projectId:guid}/plans/import")]
    public async Task<ProjectPlanImportResult> ImportPlan(Guid projectId,IFormFile file,[FromForm]string? mappingJson,CancellationToken ct)
    {if(file is null||file.Length<=0)throw new ArgumentException("Plan file is required.",nameof(file));await using var stream=file.OpenReadStream();return await plans.ImportAsync(projectId,file.FileName,stream,ProjectPlanIngestionService.ParseMappingJson(mappingJson),ct);}

    [Authorize(Policy=Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member,"projectId")]
    [HttpPost("projects/{projectId:guid}/collaboration/ingest")]
    public Task<CollaborationEvidenceResult> IngestCollaboration(Guid projectId,[FromBody]CollaborationEvidenceCommand command,CancellationToken ct)
        =>sources.IngestCollaborationAsync(command with{ProjectId=projectId},ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer,"projectId")]
    [HttpPost("projects/{projectId:guid}/entity-bindings")]
    public async Task<IActionResult> Bind(Guid projectId,[FromBody]BindProjectEntityCommand command,CancellationToken ct)
    {await resolution.BindAsync(command with{ProjectId=projectId,Actor=RequestActor.AuditIdentity(User)},ct);return NoContent();}
}
