// Purpose: Verifies P ro je ct In te ll ig en ce Co nt ra ct sT es ts behavior.
using Dip.Application.Features.ProjectIntelligence;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectIntelligence;

public sealed class ProjectIntelligenceContractsTests
{
    [Fact]
    public void ReconstructedProgress_SeparatesReportedProgressFromEvidenceReadiness()
    {
        var item = new ReconstructedProgressDto(
            "1.2", "Battle AI", "1", "Developer A", null, null,
            80m, 80m, 25m, "Low", "Watch", "Task:TASK-42", false, [],
            ["Completion is reported but independent evidence is incomplete."], [],
            [new ProgressEvidenceDto("Slack", "progress-message", "80% complete", "Low", null, DateTimeOffset.UtcNow, false)],
            []);

        item.OperationalPercent.Should().Be(80m);
        item.EvidenceReadinessPercent.Should().Be(25m);
        item.Evidence.Single().MachineObserved.Should().BeFalse();
    }

    [Fact]
    public void SourceProvenance_PreservesRevisionAndContentHash()
    {
        var p = new SourceProvenanceDto("Redmine", "531", "rev-12", "https://redmine.example/issues/531",
            DateTimeOffset.UtcNow, "Medium", "abc123");

        p.SourceId.Should().Be("531");
        p.Revision.Should().Be("rev-12");
        p.RawSha256.Should().Be("abc123");
    }
}
