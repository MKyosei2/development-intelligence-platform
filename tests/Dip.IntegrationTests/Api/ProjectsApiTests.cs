// Purpose: Verifies P ro je ct sA pi behavior, edge cases, and invariants for a pi.
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


    [Fact]
    public async Task NotificationDisposition_AppendsImmutableHistoryWithServerActor()
    {
        await using var factory = new CustomWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N");
        using var createResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"Notifications {suffix}",
                repositoryUrl = $"https://github.com/example/notifications-{suffix}",
                defaultBranch = "main",
                releaseDateUtc = (DateTimeOffset?)null
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectDto>();
        created.Should().NotBeNull();

        const string alertKey = "integration-alert";
        foreach (var disposition in new[] { "read", "acknowledge", "resolve" })
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/v1/management/projects/{created!.Id}/alerts/{alertKey}/disposition",
                new
                {
                    projectId = created.Id,
                    alertKey,
                    actor = "spoofed-client-actor",
                    disposition,
                    comment = $"{disposition} from integration test"
                });
            response.EnsureSuccessStatusCode();
        }

        var history = await client.GetFromJsonAsync<NotificationHistoryDto[]>(
            $"/api/v1/management/projects/{created!.Id}/alerts/{alertKey}/history?take=20");

        history.Should().NotBeNull();
        history!.Select(x => x.Action).Should().Contain(new[]
        {
            "notification.viewed",
            "notification.acknowledged",
            "notification.resolved"
        });
        history.Should().OnlyContain(x => x.Actor != "spoofed-client-actor");
    }

    private sealed record ProjectDto(
        Guid Id,
        string Name);

    private sealed record NotificationHistoryDto(
        Guid Id,
        string AlertKey,
        string Action,
        string Actor,
        string? Comment,
        string? DetailJson,
        DateTimeOffset AtUtc);
}
