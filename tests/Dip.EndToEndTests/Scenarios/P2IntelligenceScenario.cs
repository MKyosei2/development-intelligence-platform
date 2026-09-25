// Purpose: Verifies P 2 Intelligence Scenario behavior, edge cases, and repository invariants.
using Dip.EndToEndTests.Support;
using FluentAssertions;
using Xunit;

namespace Dip.EndToEndTests.Scenarios;

public sealed class P2IntelligenceScenario
{
    [Fact]
    public async Task IntelligenceReadModels_AreEvidenceGroundedAndDoNotInventDeviceProof()
    {
        using var client = new DipScenarioClient();
        var project = await client.CreateProjectAsync("p2-intelligence");

        var portability = await client.GetPortabilityAsync(project.Id);
        portability.Score.Should().BeInRange(0m, 100m);
        portability.EvidenceJson.Should().Contain("\"simulated\":true");
        portability.EvidenceJson.Should().Contain("\"realDevice\":false");

        var rootCause = await client.AnalyzeRootCauseAsync(project.Id);
        rootCause.Candidates.Should().NotBeEmpty();
        rootCause.Candidates[0].Evidence.Should().NotBeEmpty();

        var forecast = await client.GetForecastAsync(project.Id);
        forecast.ReleaseRiskPercent.Should().BeInRange(0m, 100m);
        forecast.Confidence.Should().Be("Low");
        forecast.EarliestCompletionUtc.Should().BeNull(
            "a new project has no Project Truth progress history from which to estimate velocity");
        forecast.LatestCompletionUtc.Should().BeNull();
        forecast.Reasons.Should().Contain(reason =>
            reason.Contains("not enough Project Truth history", StringComparison.OrdinalIgnoreCase));
    }
}
