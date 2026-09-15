using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Enterprise;

public sealed record IdentityResolutionResult(Guid? IdentityProfileId,decimal Confidence,bool RequiresApproval,string Reason);
public sealed class IdentityResolutionService(IEnterpriseIdentityRepository repo,IUnitOfWork uow,ISystemClock clock)
{
    public async Task<IdentityProfile> CreateProfileAsync(Guid organizationId,IdentityKind kind,string displayName,CancellationToken ct){var p=new IdentityProfile(Guid.NewGuid(),organizationId,kind,displayName,clock.UtcNow);await repo.AddProfileAsync(p,ct);await uow.SaveChangesAsync(ct);return p;}
    public Task<IReadOnlyList<IdentityProfile>> ListProfilesAsync(Guid organizationId,CancellationToken ct)=>repo.ListProfilesAsync(organizationId,ct);
    public async Task<ExternalIdentityLink> LinkAsync(Guid organizationId,Guid profileId,string provider,string externalId,decimal confidence,bool approved,string? approvedBy,CancellationToken ct){var profile=await repo.GetProfileAsync(profileId,ct)??throw new KeyNotFoundException("Identity profile not found.");if(profile.OrganizationId!=organizationId)throw new InvalidOperationException("Identity belongs to another organization.");var link=new ExternalIdentityLink(Guid.NewGuid(),profileId,provider,externalId,confidence,approved,approvedBy,clock.UtcNow);await repo.AddLinkAsync(link,ct);await uow.SaveChangesAsync(ct);return link;}
    public async Task<IdentityResolutionResult> ResolveAsync(Guid organizationId,string provider,string externalId,CancellationToken ct){var link=await repo.FindLinkAsync(organizationId,provider,externalId,ct);if(link is null)return new(null,0m,true,"No approved cross-source identity mapping exists.");return new(link.IdentityProfileId,link.Confidence,!link.Approved,link.Approved?"Approved identity mapping.":"Candidate mapping requires administrator approval.");}
}
