// Purpose: Verifies Development Graph persistence against the configured PostgreSQL integration-test database.
using System.Net.Http.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;
using Dip.IntegrationTests.Api;
using Dip.IntegrationTests.Database;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.IntegrationTests.DevelopmentGraph;

public sealed class DevelopmentGraphPersistenceTests :
    IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _database;

    public DevelopmentGraphPersistenceTests(PostgreSqlFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task IngestVersionControlChange_PersistsGraphAcrossScopes()
    {
        await using var factory =
            new CustomWebApplicationFactory(
                _database.ConnectionString);
        using var client = factory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N");
        using var createResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"Development Graph {suffix}",
                repositoryUrl = $"https://github.com/example/graph-{suffix}",
                defaultBranch = "main",
                releaseDateUtc = (DateTimeOffset?)null
            });

        createResponse.EnsureSuccessStatusCode();
        var project = await createResponse.Content.ReadFromJsonAsync<ProjectDto>();
        project.Should().NotBeNull();

        var repositoryKey = $"example/graph-{suffix}";
        var revision = $"revision-{suffix}";
        var change = new NormalizedVersionControlChange(
            project!.Id,
            VersionControlProvider.GitHub,
            repositoryKey,
            $"https://github.com/{repositoryKey}",
            revision,
            "main",
            "Persist development graph evidence",
            $"https://github.com/{repositoryKey}/commit/{revision}",
            DateTimeOffset.UtcNow,
            [
                new NormalizedChangedFile(
                    "src/Game/Player.cs",
                    "modified",
                    8,
                    3)
            ]);

        using (var writeScope = factory.Services.CreateScope())
        {
            var graph = writeScope.ServiceProvider
                .GetRequiredService<DevelopmentGraphService>();

            await graph.IngestVersionControlChangeAsync(
                change,
                CancellationToken.None);
        }

        using (var readScope = factory.Services.CreateScope())
        {
            var repository = readScope.ServiceProvider
                .GetRequiredService<IDevelopmentGraphRepository>();

            var nodes = await repository.ListNodesAsync(
                project.Id,
                null,
                CancellationToken.None);
            var edges = await repository.ListEdgesAsync(
                project.Id,
                CancellationToken.None);

            nodes.Should().Contain(
                node => node.Type == DevelopmentGraphNodeType.Repository
                        && node.Key == $"repo:{repositoryKey}");
            nodes.Should().Contain(
                node => node.Type == DevelopmentGraphNodeType.Branch
                        && node.Key == $"branch:{repositoryKey}:main");
            nodes.Should().Contain(
                node => node.Type == DevelopmentGraphNodeType.Commit
                        && node.Key == $"commit:{repositoryKey}:{revision}");
            nodes.Should().Contain(
                node => node.Type == DevelopmentGraphNodeType.File
                        && node.Key == $"file:{repositoryKey}:src/Game/Player.cs");
            nodes.Should().Contain(
                node => node.Type == DevelopmentGraphNodeType.Component
                        && node.Key == $"component:{repositoryKey}:src/Game");

            var commit = nodes.Single(
                node => node.Type == DevelopmentGraphNodeType.Commit
                        && node.Key == $"commit:{repositoryKey}:{revision}");
            var file = nodes.Single(
                node => node.Type == DevelopmentGraphNodeType.File
                        && node.Key == $"file:{repositoryKey}:src/Game/Player.cs");

            edges.Should().Contain(
                edge => edge.SourceNodeId == commit.Id
                        && edge.TargetNodeId == file.Id
                        && edge.Type == DevelopmentGraphEdgeType.Changes);
        }
    }

    private sealed record ProjectDto(
        Guid Id,
        string Name);
}
