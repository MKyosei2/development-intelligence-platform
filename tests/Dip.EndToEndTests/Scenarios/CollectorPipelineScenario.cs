// Purpose: Verifies C ol le ct or Pi pe li ne Sc en ar io behavior, edge cases, and invariants for s ce na ri os.
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class CollectorPipelineScenario
{
    [Fact]
    public async Task CollectorReport_ProducesPassedQualityGate()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("collector-pipeline");

        var accepted = await api.SubmitReportAsync(
            project,
            [new { name = "average_fps", value = 60m, unit = "fps", baselineValue = 60m }],
            []);

        accepted.PipelineRunId.Should().NotBeEmpty();

        var dashboard = await api.WaitForDashboardAsync(project.Id, "Passed");
        dashboard.LatestPipelineStatus.Should().Be("Succeeded");
        dashboard.QualityGateScore.Should().Be(100m);
    }
}
