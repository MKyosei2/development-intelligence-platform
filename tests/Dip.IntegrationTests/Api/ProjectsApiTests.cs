using System.Net;
using System.Net.Http.Json;
using Dip.IntegrationTests.Database;
using FluentAssertions;

namespace Dip.IntegrationTests.Api;

public sealed class ProjectsApiTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _database;

    public ProjectsApiTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task CreateThenList_ReturnsProject()
    {
        await using var factory = new CustomWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Aurora",
            repositoryUrl = "https://github.com/example/aurora",
            defaultBranch = "develop",
            releaseDateUtc = (DateTimeOffset?)null
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var projects = await client.GetFromJsonAsync<ProjectDto[]>("/api/projects");
        projects.Should().ContainSingle(x => x.Name == "Aurora");
    }

    private sealed record ProjectDto(Guid Id, string Name);
}
