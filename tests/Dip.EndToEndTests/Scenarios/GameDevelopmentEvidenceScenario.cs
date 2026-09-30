// Purpose: Verifies native/Universal-QA evidence can contradict an unsupported completion claim without relying on a real Unity or Unreal project.
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class GameDevelopmentEvidenceScenario
{
    [Fact]
    public async Task NativeQaCriticalEvidence_ContradictsCompletedClaim_AndFailsQualityGate()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("game-development-native-quality-gate");

        await api.SubmitReportAsync(
            project,
            [],
            [new
            {
                category = "memory-safety",
                severity = "Critical",
                title = "Native QA detected a memory-safety regression",
                description = "The release candidate contains a deterministic Critical finding reported by the native/Universal QA path.",
                source = "dip-native-qa",
                ruleId = "dip.native.memory-safety.critical",
                evidenceJson = "{\"scenario\":\"native-critical-contradiction\"}"
            }]);

        var failedGate = await api.WaitForDashboardAsync(project.Id, "Failed");
        failedGate.CriticalIssueCount.Should().BeGreaterThan(0);
        failedGate.QualityGateReasons.Should().Contain(
            reason => reason.Contains("Native QA detected a memory-safety regression"));

        var claim = await api.SubmitClaimAsync(project, "Complete", 100m);
        var truth = await api.GetDashboardAsync(project.Id);

        claim.VerificationStatus.Should().Be("Contradicted");
        truth.ReportedProgress.Should().Be(100m);
        truth.VerifiedProgress.Should().BeLessThan(truth.ReportedProgress!.Value);
        truth.ContradictionCount.Should().BeGreaterThan(0);
        truth.RiskLevel.Should().BeOneOf("High", "Critical");
        truth.TruthStatus.Should().BeOneOf("AtRisk", "Blocked");
    }
}
