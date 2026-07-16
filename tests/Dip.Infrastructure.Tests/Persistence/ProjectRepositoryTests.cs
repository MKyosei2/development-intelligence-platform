using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Tests.Persistence;

public sealed class ProjectRepositoryTests
{
    [Fact]
    public async Task AddAndGet_RoundTripsProject()
    {
        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new DipDbContext(options);
        var repository = new ProjectRepository(db);

        var project = new Project(
            Guid.NewGuid(),
            "Aurora",
            "https://github.com/example/aurora",
            "develop",
            DateTimeOffset.UtcNow);

        await repository.AddAsync(project, CancellationToken.None);
        await db.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(project.Id, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Aurora");
    }
}
