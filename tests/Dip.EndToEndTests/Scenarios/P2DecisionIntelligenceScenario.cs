// Purpose: Defines P2 Decision Intelligence Scenario responsibilities for this repository area.
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class P2DecisionIntelligenceScenario
{
    [Fact]
    public async Task ObjectiveEvidence_FlowsIntoP2DecisionBriefAndDailyChange()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("p2-decision-intelligence");

        await api.SubmitReportAsync(
            project,
            [],
            [new
            {
                category = "runtime",
                severity = "Critical",
                title = "Representative runtime blocker",
                description = "P2 E2E objective evidence.",
                source = "e2e/runtime",
                ruleId = "e2e.runtime.blocker",
                evidenceJson = (string?)null
            }]);

        await api.SubmitClaimAsync(project, "Complete", 100m);

        var daily = await api.GetP2DailyChangeAsync(project);
        daily.NewEvidence.Should().BeGreaterThan(0);
        daily.NewFailures.Should().BeGreaterThan(0);

        var brief = await api.GetP2DecisionBriefAsync(project);
        brief.ProjectId.Should().Be(project.Id);
        brief.Summary.Should().NotBeNullOrWhiteSpace();
        brief.Priorities.Should().NotBeEmpty();
        brief.OverallStatus.Should().Be("ATTENTION_REQUIRED");

        var health = await api.GetP2HealthChangeAsync(project);
        health.CurrentHealth.Should().NotBeNull();
    }
}
