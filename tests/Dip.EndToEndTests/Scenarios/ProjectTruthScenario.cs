// Purpose: Verifies behavior and invariants covered by P ro je ct Tr ut hS ce na ri o.
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class ProjectTruthScenario
{
    [Fact]
    public async Task SuccessfulEvidence_CanVerifyCompletedProjectClaim()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("project-truth-confirmed");

        await api.SubmitReportAsync(project, [], []);
        await api.WaitForDashboardAsync(project.Id, "Passed");
        await api.SubmitClaimAsync(project, "Complete", 100m);

        var dashboard = await api.GetDashboardAsync(project.Id);

        dashboard.ReportedProgress.Should().Be(100m);
        dashboard.VerifiedProgress.Should().Be(100m);
        dashboard.ContradictionCount.Should().Be(0);
        dashboard.TruthStatus.Should().Be("Healthy");
        dashboard.EvidenceConfidence.Should().Be("High");
    }

    [Fact]
    public async Task CriticalEvidence_ContradictsCompletedProjectClaim()
    {
        using var api = new DipScenarioClient();
        var project = await api.CreateProjectAsync("project-truth-contradicted");

        await api.SubmitReportAsync(
            project,
            [],
            [new
            {
                category = "runtime",
                severity = "Critical",
                title = "Runtime crash",
                description = "The current build crashes during the required scenario.",
                source = "runtime-e2e",
                ruleId = "runtime.crash",
                evidenceJson = (string?)null
            }]);

        await api.WaitForDashboardAsync(project.Id, "Failed");
        var claim = await api.SubmitClaimAsync(project, "Complete", 100m);
        var dashboard = await api.GetDashboardAsync(project.Id);

        claim.VerificationStatus.Should().Be("Contradicted");
        dashboard.ReportedProgress.Should().Be(100m);
        dashboard.VerifiedProgress.Should().Be(0m);
        dashboard.ContradictionCount.Should().BeGreaterThan(0);
        dashboard.RiskLevel.Should().BeOneOf("High", "Critical");
        dashboard.TruthStatus.Should().BeOneOf("AtRisk", "Blocked");
    }
}
