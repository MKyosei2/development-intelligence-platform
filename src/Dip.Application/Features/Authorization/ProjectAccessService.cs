using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Authorization;

public sealed record ProjectMembershipDto(Guid Id,Guid ProjectId,string SubjectId,string Login,ProjectMembershipRole Role,string GrantedBy,DateTimeOffset CreatedAtUtc,DateTimeOffset UpdatedAtUtc);

public sealed class ProjectAccessService(IProjectMembershipRepository memberships,IAuditLogRepository audit,IUnitOfWork uow,ISystemClock clock,IProjectMutationCoordinator mutations)
{
    public async Task<bool> HasAccessAsync(Guid projectId,string subjectId,ProjectMembershipRole minimum,CancellationToken ct){var membership=await memberships.FindAsync(projectId,subjectId,ct);return membership is not null&&Rank(membership.Role)>=Rank(minimum);}
    public async Task<IReadOnlySet<Guid>> AccessibleProjectsAsync(string subjectId,CancellationToken ct)=>(await memberships.ListBySubjectAsync(subjectId,ct)).Select(x=>x.ProjectId).ToHashSet();
    public Task GrantOwnerAsync(Guid projectId,string subjectId,string login,string actor,CancellationToken ct)=>UpsertAsync(projectId,subjectId,login,ProjectMembershipRole.Owner,actor,"Initial project owner",ct);
    public Task UpsertAsync(Guid projectId,string subjectId,string login,ProjectMembershipRole role,string actor,string reason,CancellationToken ct)=>mutations.ExecuteAsync(projectId,async token=>
    {
        var existing=await memberships.FindAsync(projectId,subjectId,token);var before=existing is null?null:JsonSerializer.Serialize(new{existing.SubjectId,existing.Login,existing.Role});
        if(existing is null){existing=new ProjectMembership(Guid.NewGuid(),projectId,subjectId,login,role,actor,clock.UtcNow);await memberships.AddAsync(existing,token);}else existing.ChangeRole(role,actor,clock.UtcNow);
        await audit.AddAsync(new AuditLog(Guid.NewGuid(),projectId,"authorization","membership.upsert",actor,subjectId,JsonSerializer.Serialize(new{before,after=new{existing.SubjectId,existing.Login,existing.Role}}),clock.UtcNow,reason:reason),token);await uow.SaveChangesAsync(token);
    },ct);
    public Task RemoveAsync(Guid projectId,string subjectId,string actor,string reason,CancellationToken ct)=>mutations.ExecuteAsync(projectId,async token=>
    {
        var existing=await memberships.FindAsync(projectId,subjectId,token)??throw new KeyNotFoundException("Project membership not found.");if(existing.Role==ProjectMembershipRole.Owner){var all=await memberships.ListByProjectAsync(projectId,token);if(all.Count(x=>x.Role==ProjectMembershipRole.Owner)<=1)throw new InvalidOperationException("A project must retain at least one owner.");}
        memberships.Remove(existing);await audit.AddAsync(new AuditLog(Guid.NewGuid(),projectId,"authorization","membership.remove",actor,subjectId,JsonSerializer.Serialize(new{existing.SubjectId,existing.Login,existing.Role}),clock.UtcNow,reason:reason),token);await uow.SaveChangesAsync(token);
    },ct);
    public async Task<IReadOnlyList<ProjectMembershipDto>> ListAsync(Guid projectId,CancellationToken ct)=>(await memberships.ListByProjectAsync(projectId,ct)).Select(Map).ToArray();
    private static int Rank(ProjectMembershipRole role)=>role switch{ProjectMembershipRole.Viewer=>0,ProjectMembershipRole.Member=>1,ProjectMembershipRole.Maintainer=>2,ProjectMembershipRole.Owner=>3,_=>-1};
    private static ProjectMembershipDto Map(ProjectMembership x)=>new(x.Id,x.ProjectId,x.SubjectId,x.Login,x.Role,x.GrantedBy,x.CreatedAtUtc,x.UpdatedAtUtc);
}
