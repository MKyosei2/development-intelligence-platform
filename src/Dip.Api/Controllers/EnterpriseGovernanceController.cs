// Purpose: Exposes E nt er pr is eG ov er na nc eC on tr ol le r HTTP endpoints.
using Dip.Api.Authorization;
using Dip.Application.Features.Enterprise;
using Dip.Application.Features.Evidence;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
public sealed record IntegrityAppendRequest(Guid OrganizationId,Guid EvidenceId,string PayloadJson,string? SignatureBase64,string? SourceKeyId,string? PublicKeyPem);
public sealed record QueueEvidenceRequest(Guid OrganizationId,string IdempotencyKey,long SourceSequence,IngestEvidenceCommand Evidence);
public sealed record CreateIdentityRequest(IdentityKind Kind,string DisplayName);
public sealed record LinkIdentityRequest(Guid ProfileId,string Provider,string ExternalId,decimal Confidence,bool Approved,string? ApprovedBy);
public sealed record GrantRoleRequest(string SubjectId,OrganizationRole Role,Guid? ProjectId,string GrantedBy);

[ApiController][Route("api/v1/enterprise/governance")]
public sealed class EnterpriseGovernanceController(EvidenceIntegrityService integrity,DistributedIngestionService ingestion,IdentityResolutionService identities,EnterpriseTenantService tenants,RetentionGovernanceService retention,AdaptiveQaRoutingService adaptive):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("projects/{projectId:guid}/integrity")] public async Task<IActionResult> Append(Guid projectId,IntegrityAppendRequest r,CancellationToken ct)=>Ok(await integrity.AppendAsync(new(r.OrganizationId,projectId,r.EvidenceId,r.PayloadJson,r.SignatureBase64,r.SourceKeyId,r.PublicKeyPem),ct));
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/integrity")] public Task<IReadOnlyList<Dip.Domain.Entities.EvidenceIntegrityReceipt>> Receipts(Guid projectId,[FromQuery]int take,CancellationToken ct)=>integrity.ListAsync(projectId,take<=0?100:take,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/integrity/verify")] public Task<bool> Verify(Guid projectId,CancellationToken ct)=>integrity.VerifyChainAsync(projectId,ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("projects/{projectId:guid}/ingestion")] public Task<QueueEvidenceResult> Queue(Guid projectId,QueueEvidenceRequest r,CancellationToken ct)=>ingestion.EnqueueAsync(new(r.OrganizationId,projectId,r.IdempotencyKey,r.SourceSequence,r.Evidence),ct);
    [Authorize(Policy=Policies.Admin)][HttpPost("organizations/{organizationId:guid}/ingestion/drain")] public Task<int> Drain(Guid organizationId,[FromQuery]int take,CancellationToken ct)=>ingestion.DrainAsync(organizationId,take<=0?100:take,ct);
    [Authorize(Policy=Policies.Admin)][HttpPost("organizations/{organizationId:guid}/identities")] public async Task<IActionResult> CreateIdentity(Guid organizationId,CreateIdentityRequest r,CancellationToken ct)=>Ok(await identities.CreateProfileAsync(organizationId,r.Kind,r.DisplayName,ct));
    [Authorize(Policy=Policies.Admin)][HttpGet("organizations/{organizationId:guid}/identities")] public Task<IReadOnlyList<Dip.Domain.Entities.IdentityProfile>> Identities(Guid organizationId,CancellationToken ct)=>identities.ListProfilesAsync(organizationId,ct);
    [Authorize(Policy=Policies.Admin)][HttpPost("organizations/{organizationId:guid}/identities/link")] public async Task<IActionResult> Link(Guid organizationId,LinkIdentityRequest r,CancellationToken ct)=>Ok(await identities.LinkAsync(organizationId,r.ProfileId,r.Provider,r.ExternalId,r.Confidence,r.Approved,r.ApprovedBy,ct));
    [Authorize(Policy=Policies.Admin)][HttpPost("organizations/{organizationId:guid}/roles")] public async Task<IActionResult> Grant(Guid organizationId,GrantRoleRequest r,CancellationToken ct){await tenants.GrantRoleAsync(organizationId,r.SubjectId,r.Role,r.ProjectId,r.GrantedBy,ct);return NoContent();}
    [Authorize(Policy=Policies.Admin)][HttpGet("organizations/{organizationId:guid}/retention/preview")] public Task<RetentionPreview> RetentionPreview(Guid organizationId,[FromQuery]Guid? projectId,CancellationToken ct)=>retention.PreviewAsync(organizationId,projectId,null,ct);
    [Authorize(Policy=Policies.Admin)][HttpPost("organizations/{organizationId:guid}/retention/execute")] public Task<int> RetentionExecute(Guid organizationId,[FromQuery]Guid? projectId,CancellationToken ct)=>retention.ExecuteCompletedQueueRetentionAsync(organizationId,projectId,User.Identity?.Name??"admin",ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("adaptive-qa")] public IReadOnlyList<AdaptiveQaSelection> AdaptiveQa([FromQuery]EvidenceSourceType source,[FromQuery]EvidenceEventType eventType,[FromQuery]string? subjectKey,[FromQuery]string? metadataJson)=>adaptive.Select(source,eventType,subjectKey,metadataJson);
}
