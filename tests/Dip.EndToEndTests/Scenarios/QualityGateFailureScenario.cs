// Purpose: Verifies behavior and invariants covered by Q ua li ty Ga te Fa il ur eS ce na ri o.
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class QualityGateFailureScenario
{
    [Fact]
    public async Task CriticalIssue_ProducesFailedQualityGate()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("quality-failure");

        await api.SubmitReportAsync(
            project,
            [],
            [new
            {
                category = "build",
                severity = "Critical",
                title = "Build failed",
                description = "Release build returned a non-zero exit code.",
                source = "ci",
                ruleId = "build.failed",
                evidenceJson = (string?)null
            }]);

        var dashboard = await api.WaitForDashboardAsync(project.Id, "Failed");
        dashboard.CriticalIssueCount.Should().BeGreaterThan(0);
        dashboard.QualityGateReasons.Should().Contain(x => x.Contains("Build failed"));
    }
}
