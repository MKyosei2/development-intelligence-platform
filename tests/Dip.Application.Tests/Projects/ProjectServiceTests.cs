// Purpose: Verifies project creation, lifecycle transitions, authorization rules, and in-memory persistence behavior.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Projects;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
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
            new CreateProjectCommand(
                "Aurora",
                "https://github.com/example/aurora",
                "develop",
                null),
            ownerSubject: "subject-1",
            ownerLogin: "owner",
            actor: "test",
            ct: CancellationToken.None);

        result.Name.Should().Be("Aurora");
        repository.Items.Should().ContainSingle();
        memberships.Items.Should().ContainSingle(x => x.ProjectId == result.Id);
        audit.Items.Should().ContainSingle(x => x.ProjectId == result.Id);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Lifecycle_RoundTrip_IsDerivedFromAuditHistory()
    {
        var repository = new InMemoryProjectRepository();
        var memberships = new InMemoryProjectMembershipRepository();
        var audit = new InMemoryAuditLogRepository();
        var unitOfWork = new StubUnitOfWork();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        var service = new ProjectService(repository, memberships, audit, unitOfWork, clock);

        var created = await service.CreateAsync(
            new CreateProjectCommand(
                "Lifecycle",
                "https://github.com/example/lifecycle",
                "main",
                null),
            "subject-1",
            "owner",
            "test",
            CancellationToken.None);

        var archived = await service.ArchiveAsync(
            created.Id,
            "test",
            "finished",
            CancellationToken.None);

        archived.LifecycleState.Should().Be(ProjectService.ArchivedState);
        (await service.GetLifecycleStateAsync(created.Id, CancellationToken.None))
            .Should()
            .Be(ProjectService.ArchivedState);

        var restored = await service.RestoreAsync(
            created.Id,
            "test",
            "maintenance",
            CancellationToken.None);

        restored.LifecycleState.Should().Be(ProjectService.ActiveState);
        (await service.GetLifecycleStateAsync(created.Id, CancellationToken.None))
            .Should()
            .Be(ProjectService.ActiveState);

        var deleted = await service.DeleteAsync(
            created.Id,
            "test",
            "retired",
            CancellationToken.None);

        deleted.LifecycleState.Should().Be(ProjectService.DeletedState);
        (await service.GetLifecycleStateAsync(created.Id, CancellationToken.None))
            .Should()
            .Be(ProjectService.DeletedState);

        audit.Items.Should().Contain(x => x.Action == "project.archived");
        audit.Items.Should().Contain(x => x.Action == "project.restored");
        audit.Items.Should().Contain(x => x.Action == "project.deleted");
    }

    [Fact]
    public async Task UpsertAsync_CannotDemoteLastOwner()
    {
        var projectId = Guid.NewGuid();
        var memberships = new InMemoryProjectMembershipRepository();
        var audit = new InMemoryAuditLogRepository();
        var unitOfWork = new StubUnitOfWork();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));
        var owner = new ProjectMembership(
            Guid.NewGuid(),
            projectId,
            "owner-subject",
            "owner",
            ProjectMembershipRole.Owner,
            "seed",
            clock.UtcNow);

        memberships.Items.Add(owner);

        var access = new ProjectAccessService(
            memberships,
            audit,
            unitOfWork,
            clock,
            new StubMutationCoordinator());

        Func<Task> act = () => access.UpsertAsync(
            projectId,
            owner.SubjectId,
            owner.Login,
            ProjectMembershipRole.Viewer,
            "test",
            "attempt demotion",
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*last project owner cannot be demoted*");

        owner.Role.Should().Be(ProjectMembershipRole.Owner);
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        public List<Project> Items { get; } = [];

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Project>>(Items);

        public Task AddAsync(Project project, CancellationToken ct)
        {
            Items.Add(project);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProjectMembershipRepository : IProjectMembershipRepository
    {
        public List<ProjectMembership> Items { get; } = [];

        public Task<ProjectMembership?> FindAsync(
            Guid projectId,
            string subjectId,
            CancellationToken ct) =>
            Task.FromResult(
                Items.SingleOrDefault(
                    x => x.ProjectId == projectId && x.SubjectId == subjectId));

        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                Items.Where(x => x.ProjectId == projectId).ToArray());

        public Task<IReadOnlyList<ProjectMembership>> ListBySubjectAsync(
            string subjectId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                Items.Where(x => x.SubjectId == subjectId).ToArray());

        public Task AddAsync(ProjectMembership membership, CancellationToken ct)
        {
            Items.Add(membership);
            return Task.CompletedTask;
        }

        public void Remove(ProjectMembership membership)
        {
            Items.Remove(membership);
        }
    }

    private sealed class InMemoryAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLog> Items { get; } = [];

        public Task AddAsync(AuditLog entry, CancellationToken ct)
        {
            Items.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditLog>> ListByProjectAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuditLog>>(
                Items
                    .Where(x => x.ProjectId == projectId)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .ThenByDescending(x => x.Id)
                    .Take(Math.Clamp(take, 1, 5000))
                    .ToArray());

        public Task<IReadOnlyList<AuditLog>> SearchAsync(
            Guid projectId,
            string? search,
            string? category,
            string? correlationId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int take,
            CancellationToken ct)
        {
            var query = Filter(projectId, search, category, correlationId, fromUtc, toUtc)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(Math.Clamp(take, 1, 5000))
                .ToArray();

            return Task.FromResult<IReadOnlyList<AuditLog>>(query);
        }

        public async Task<AuditLogPage> SearchPageAsync(
            Guid projectId,
            string? search,
            string? category,
            string? correlationId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            DateTimeOffset? beforeUtc,
            Guid? beforeId,
            int take,
            CancellationToken ct)
        {
            var limit = Math.Clamp(take, 1, 500);
            var query = await SearchAsync(
                projectId,
                search,
                category,
                correlationId,
                fromUtc,
                toUtc,
                5000,
                ct);

            var rows = query
                .Where(x => !beforeUtc.HasValue
                            || x.CreatedAtUtc < beforeUtc.Value
                            || (x.CreatedAtUtc == beforeUtc.Value
                                && (!beforeId.HasValue || x.Id.CompareTo(beforeId.Value) < 0)))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(limit + 1)
                .ToArray();

            var page = rows.Take(limit).ToArray();
            var last = rows.Length > limit ? page.LastOrDefault() : null;
            return new AuditLogPage(page, last?.CreatedAtUtc, last?.Id);
        }

        public Task<AuditLog?> GetLatestByCategoryAsync(
            Guid projectId,
            string category,
            CancellationToken ct) =>
            Task.FromResult(
                Items
                    .Where(x => x.ProjectId == projectId
                                && x.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .ThenByDescending(x => x.Id)
                    .FirstOrDefault());

        public Task<IReadOnlyDictionary<Guid, AuditLog>> GetLatestByCategoryAsync(
            IReadOnlyCollection<Guid> projectIds,
            string category,
            CancellationToken ct)
        {
            var idSet = projectIds.ToHashSet();
            IReadOnlyDictionary<Guid, AuditLog> result = Items
                .Where(x => idSet.Contains(x.ProjectId)
                            && x.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .GroupBy(x => x.ProjectId)
                .ToDictionary(group => group.Key, group => group.First());

            return Task.FromResult(result);
        }

        private IEnumerable<AuditLog> Filter(
            Guid projectId,
            string? search,
            string? category,
            string? correlationId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc)
        {
            return Items.Where(x =>
                x.ProjectId == projectId
                && (string.IsNullOrWhiteSpace(search)
                    || x.Action.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.Actor.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.SubjectKey?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                && (string.IsNullOrWhiteSpace(category)
                    || x.Category.Contains(category, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(correlationId)
                    || (x.CorrelationId?.Contains(correlationId, StringComparison.OrdinalIgnoreCase) ?? false))
                && (!fromUtc.HasValue || x.CreatedAtUtc >= fromUtc.Value)
                && (!toUtc.HasValue || x.CreatedAtUtc <= toUtc.Value));
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class StubMutationCoordinator : IProjectMutationCoordinator
    {
        public Task<T> ExecuteAsync<T>(
            Guid projectId,
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct) =>
            action(ct);

        public Task ExecuteAsync(
            Guid projectId,
            Func<CancellationToken, Task> action,
            CancellationToken ct) =>
            action(ct);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
