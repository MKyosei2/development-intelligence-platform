// Purpose: Verifies finding post-processing remains side-effect free for an empty finding set.
using Dip.Application.Features.QualityAutomation;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaFindingProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WithNoFindings_WritesIntelligenceArtifactAndReturnsZeroDecisions()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-").FullName;
        await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
        var sut = new QaFindingProcessor(null!, null!, null!, null!, null!, null!);

        var result = await sut.ProcessAsync(plan, Guid.NewGuid(), [], CancellationToken.None);

        result.ReleaseBlockers.Should().Be(0);
        result.P0Decisions.Should().Be(0);
        File.Exists(Path.Combine(plan.ArtifactRoot, "qa-intelligence.json")).Should().BeTrue();
    }
}
