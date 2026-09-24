// Purpose: Verifies D ai ly Su mm ar yS ce na ri o behavior, edge cases, and invariants for s ce na ri os.
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class DailySummaryScenario
{
    [Fact]
    public async Task DashboardProjectList_ReflectsLatestWarningGate()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("dashboard-summary");

        await api.SubmitReportAsync(
            project,
            [],
            [new
            {
                category = "asset",
                severity = "Warning",
                title = "Oversized texture",
                description = "Texture exceeds the configured budget.",
                source = "Assets/Hero.png",
                ruleId = "asset.texture-size",
                evidenceJson = (string?)null
            }]);

        await api.WaitForDashboardAsync(project.Id, "Warning");
        var projects = await api.GetDashboardProjectsAsync();

        projects.Should().Contain(x =>
            x.ProjectId == project.Id &&
            x.LatestPipelineStatus == "Succeeded" &&
            x.LatestQualityGateStatus == "Warning");
    }
}
