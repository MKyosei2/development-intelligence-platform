// Purpose: Verifies expected behavior and invariants for the surrounding feature.
using Dip.Application.Features.Dashboard;
using FluentAssertions;

namespace Dip.Application.Tests;

public sealed class DashboardTests
{
    [Fact]
    public async Task GetProjectsAsync_ReturnsRepositoryData()
    {
        var projectId = Guid.NewGuid();
        var expected = new ProjectDashboardSummary(
            projectId,
            "Aurora",
            "example/aurora",
            "main",
            92m,
            "Healthy",
            "Low",
            80m,
            90m,
            "High",
            0,
            0,
            "Succeeded",
            "Passed",
            DateTimeOffset.UtcNow);

        var repository = new FakeDashboardRepository([expected], null);
        var service = new DashboardQueryService(repository);

        var result = await service.GetProjectsAsync(CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public async Task GetProjectAsync_ReturnsDetails()
    {
        var projectId = Guid.NewGuid();
        var expected = CreateDetails(projectId);
        var service = new DashboardQueryService(
            new FakeDashboardRepository([], expected));

        var result = await service.GetProjectAsync(projectId, CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetProjectAsync_RejectsEmptyProjectId()
    {
        var service = new DashboardQueryService(
            new FakeDashboardRepository([], null));

        var action = () => service.GetProjectAsync(Guid.Empty, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("projectId");
    }

    private static ProjectDashboardDetails CreateDetails(Guid projectId) =>
        new(
            projectId,
            "Aurora",
            "example/aurora",
            "main",
            92m,
            "Healthy",
            "Low",
            80m,
            90m,
            "High",
            0,
            0,
            0,
            "Evidence supports the current state.",
            ["No immediate action is required."],
            [],
            [],
            [],
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "Succeeded",
            DateTimeOffset.UtcNow,
            "Passed",
            100m,
            0,
            0,
            ["No blocking quality issues."],
            [new DashboardMetricPoint("average_fps", 60m, "fps", 0m)],
            []);

    private sealed class FakeDashboardRepository(
        IReadOnlyList<ProjectDashboardSummary> projects,
        ProjectDashboardDetails? details)
        : IDashboardReadRepository
    {
        public Task<IReadOnlyList<ProjectDashboardSummary>> GetProjectsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(projects);

        public Task<ProjectDashboardDetails?> GetProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(details);
    }
}
