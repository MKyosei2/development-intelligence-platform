// Purpose: Verifies Game Development Evidence Scenario behavior, edge cases, and repository invariants.
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class GameDevelopmentEvidenceScenario
{
    [Fact]
    public async Task UnityCriticalEvidence_ContradictsCompletedClaim_AndFailsQualityGate()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("game-development-unity-quality-gate");

        await api.SubmitReportAsync(
            project,
            [],
            [new
            {
                category = "asset",
                severity = "Critical",
                title = "Unity scene contains a missing reference",
                description = "The release candidate contains a missing scene reference reported by the Unity collector.",
                source = "unity-collector",
                ruleId = "unity.scene.missing-reference",
                evidenceJson = (string?)null
            }]);

        var failedGate = await api.WaitForDashboardAsync(project.Id, "Failed");
        failedGate.CriticalIssueCount.Should().BeGreaterThan(0);
        failedGate.QualityGateReasons.Should().Contain(
            reason => reason.Contains("Unity scene contains a missing reference"));

        var claim = await api.SubmitClaimAsync(project, "Complete", 100m);
        var truth = await api.GetDashboardAsync(project.Id);

        claim.VerificationStatus.Should().Be("Contradicted");
        truth.ReportedProgress.Should().Be(100m);
        truth.VerifiedProgress.Should().Be(0m);
        truth.ContradictionCount.Should().BeGreaterThan(0);
        truth.RiskLevel.Should().BeOneOf("High", "Critical");
        truth.TruthStatus.Should().BeOneOf("AtRisk", "Blocked");
    }
}
