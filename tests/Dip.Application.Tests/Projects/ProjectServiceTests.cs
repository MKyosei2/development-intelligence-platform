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
        var unitOfWork = new StubUnitOfWork();
        var clock = new FixedClock(
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero));
        var service = new ProjectService(repository, unitOfWork, clock);

        var result = await service.CreateAsync(
            new CreateProjectCommand(
                "Aurora",
                "https://github.com/example/aurora",
                "develop",
                null),
            CancellationToken.None);

        result.Name.Should().Be("Aurora");
        repository.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(1);
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

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
