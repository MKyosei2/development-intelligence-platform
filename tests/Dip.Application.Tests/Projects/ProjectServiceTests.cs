using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Projects;
using Dip.Domain.Entities;
using FluentAssertions;

namespace Dip.Application.Tests.Projects;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsProject()
    {
        var repository = new InMemoryProjectRepository();
        var memberships = new InMemoryProjectMembershipRepository();
        var audit = new InMemoryAuditLogRepository();
        var unitOfWork = new StubUnitOfWork();
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero));
        var service = new ProjectService(repository, memberships, audit, unitOfWork, clock);
        var result = await service.CreateAsync(
            new CreateProjectCommand("Aurora", "https://github.com/example/aurora", "develop", null),
            ownerSubject: "subject-1", ownerLogin: "owner", actor: "test", ct: CancellationToken.None);
        result.Name.Should().Be("Aurora");
        repository.Items.Should().ContainSingle();
        memberships.Items.Should().ContainSingle(x => x.ProjectId == result.Id);
        audit.Items.Should().ContainSingle(x => x.ProjectId == result.Id);
        unitOfWork.SaveCount.Should().Be(1);
    }
    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        public List<Project> Items { get; } = [];
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)=>Task.FromResult(Items.SingleOrDefault(x=>x.Id==id));
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<Project>>(Items);
        public Task AddAsync(Project project,CancellationToken ct){Items.Add(project);return Task.CompletedTask;}
    }
    private sealed class InMemoryProjectMembershipRepository : IProjectMembershipRepository
    {
        public List<ProjectMembership> Items { get; }=[];
        public Task<ProjectMembership?> FindAsync(Guid projectId,string subjectId,CancellationToken ct)=>Task.FromResult(Items.SingleOrDefault(x=>x.ProjectId==projectId&&x.SubjectId==subjectId));
        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(Guid projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ProjectMembership>>(Items.Where(x=>x.ProjectId==projectId).ToArray());
        public Task<IReadOnlyList<ProjectMembership>> ListBySubjectAsync(string subjectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ProjectMembership>>(Items.Where(x=>x.SubjectId==subjectId).ToArray());
        public Task AddAsync(ProjectMembership membership,CancellationToken ct){Items.Add(membership);return Task.CompletedTask;}
        public void Remove(ProjectMembership membership)=>Items.Remove(membership);
    }
    private sealed class InMemoryAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLog> Items { get; }=[];
        public Task AddAsync(AuditLog entry,CancellationToken ct){Items.Add(entry);return Task.CompletedTask;}
        public Task<IReadOnlyList<AuditLog>> ListByProjectAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<AuditLog>>(Items.Where(x=>x.ProjectId==projectId).Take(take).ToArray());
    }
    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken){SaveCount++;return Task.FromResult(1);}
    }
    private sealed class FixedClock(DateTimeOffset utcNow):ISystemClock{public DateTimeOffset UtcNow{get;}=utcNow;}
}
