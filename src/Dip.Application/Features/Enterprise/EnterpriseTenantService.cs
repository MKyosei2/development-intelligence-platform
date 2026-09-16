// Purpose: Coordinates E nt er pr is eT en an tS er vi ce application behavior for Enterprise.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Enterprise;

public sealed class EnterpriseTenantService(IEnterpriseFleetRepository fleet,IEnterpriseIdentityRepository identities,IUnitOfWork uow,ISystemClock clock)
{
    public async Task<OrganizationTenant> CreateOrganizationAsync(string name,string slug,CancellationToken ct){var o=new OrganizationTenant(Guid.NewGuid(),name,slug,clock.UtcNow);await fleet.AddOrganizationAsync(o,ct);await uow.SaveChangesAsync(ct);return o;}
    public Task<IReadOnlyList<OrganizationTenant>> ListOrganizationsAsync(CancellationToken ct)=>fleet.ListOrganizationsAsync(ct);
    public async Task BindProjectAsync(Guid organizationId,Guid projectId,string? portfolioKey,CancellationToken ct){if(await fleet.GetProjectBindingAsync(projectId,ct) is not null)throw new InvalidOperationException("Project already belongs to an organization.");await fleet.AddProjectBindingAsync(new ProjectTenantBinding(Guid.NewGuid(),organizationId,projectId,portfolioKey,clock.UtcNow),ct);await uow.SaveChangesAsync(ct);}
    public Task<ProjectTenantBinding?> ResolveProjectAsync(Guid projectId,CancellationToken ct)=>fleet.GetProjectBindingAsync(projectId,ct);
    public async Task GrantRoleAsync(Guid organizationId,string subjectId,OrganizationRole role,Guid? projectId,string actor,CancellationToken ct){await identities.AddRoleAsync(new OrganizationRoleBinding(Guid.NewGuid(),organizationId,subjectId,role,projectId,actor,clock.UtcNow),ct);await uow.SaveChangesAsync(ct);}
    public async Task<bool> HasRoleAsync(Guid organizationId,string subjectId,Guid? projectId,OrganizationRole minimum,CancellationToken ct){var roles=await identities.ListRolesAsync(organizationId,subjectId,ct);return roles.Any(x=>(x.ProjectId is null||x.ProjectId==projectId)&&Rank(x.Role)>=Rank(minimum));}
    private static int Rank(OrganizationRole role)=>role switch{OrganizationRole.OrganizationAdmin=>100,OrganizationRole.Producer=>90,OrganizationRole.ProjectManager=>80,OrganizationRole.Lead=>70,OrganizationRole.Developer or OrganizationRole.Artist or OrganizationRole.Qa=>50,OrganizationRole.Viewer=>10,OrganizationRole.Agent=>5,_=>0};
}
