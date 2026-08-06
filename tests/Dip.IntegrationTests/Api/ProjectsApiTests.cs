using System.Net;
using System.Net.Http.Json;
using Dip.IntegrationTests.Database;
using FluentAssertions;

namespace Dip.IntegrationTests.Api;

public sealed class ProjectsApiTests :
    IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _database;

    public ProjectsApiTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task CreateThenList_ReturnsCreatedProject()
    {
        await using var factory =
            new CustomWebApplicationFactory(
                _database.ConnectionString);

        using var client = factory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N");
        var repositoryUrl =
            $"https://github.com/example/aurora-{suffix}";

        using var createResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"Aurora {suffix}",
                repositoryUrl,
                defaultBranch = "develop",
                releaseDateUtc = (DateTimeOffset?)null
            });

        createResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.Created);

        var created = await createResponse.Content
            .ReadFromJsonAsync<ProjectDto>();

        created.Should().NotBeNull();

        var projects = await client
            .GetFromJsonAsync<ProjectDto[]>("/api/projects");

        projects.Should().NotBeNull();
        projects.Should().Contain(
            project => project.Id == created!.Id);
    }

    private sealed record ProjectDto(
        Guid Id,
        string Name);
}
