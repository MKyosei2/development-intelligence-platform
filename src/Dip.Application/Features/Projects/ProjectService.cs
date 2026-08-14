using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Projects;
public sealed record CreateProjectCommand(string Name,string RepositoryUrl,string DefaultBranch,DateTimeOffset? ReleaseDateUtc);
public sealed record ProjectDto(Guid Id,string Name,string RepositoryUrl,string DefaultBranch,decimal HealthScore,DateTimeOffset? ReleaseDateUtc);
public sealed class ProjectService(IProjectRepository projects,IProjectMembershipRepository memberships,IAuditLogRepository audit,IUnitOfWork uow,ISystemClock clock)
{
    public async Task<ProjectDto>CreateAsync(CreateProjectCommand command,string ownerSubject,string ownerLogin,string actor,CancellationToken ct)
    {
        var p=new Project(Guid.NewGuid(),command.Name,command.RepositoryUrl,command.DefaultBranch,clock.UtcNow);p.SetReleaseDate(command.ReleaseDateUtc);await projects.AddAsync(p,ct);
        var membership=new ProjectMembership(Guid.NewGuid(),p.Id,ownerSubject,ownerLogin,ProjectMembershipRole.Owner,actor,clock.UtcNow);await memberships.AddAsync(membership,ct);
        await audit.AddAsync(new AuditLog(Guid.NewGuid(),p.Id,"authorization","project.provision",actor,p.Id.ToString(),JsonSerializer.Serialize(new{ownerSubject,ownerLogin,role=ProjectMembershipRole.Owner.ToString()}),clock.UtcNow,reason:"Project created with an explicit owner membership."),ct);
        await uow.SaveChangesAsync(ct);return Map(p);
    }
    public async Task<IReadOnlyList<ProjectDto>>ListAsync(CancellationToken ct)=>(await projects.ListAsync(ct)).Select(Map).ToArray();
    public async Task<ProjectDto?>GetAsync(Guid id,CancellationToken ct){var p=await projects.GetByIdAsync(id,ct);return p is null?null:Map(p);}
    private static ProjectDto Map(Project p)=>new(p.Id,p.Name,p.RepositoryUrl,p.DefaultBranch,p.HealthScore.Value,p.ReleaseDateUtc);
}
