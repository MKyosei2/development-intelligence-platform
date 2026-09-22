// Purpose: Verifies adapter execution materializes successful direct adapter results without false failures.
using Dip.Application.Features.QualityAutomation;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaAdapterExecutionCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoopAdapter_ProducesOneSuccessfulOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-adapter-").FullName;
        await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
        var sut = new QaAdapterExecutionCoordinator(null!, Array.Empty<ICrossCuttingQaPack>());

        var result = await sut.ExecuteAsync(plan, CancellationToken.None);

        result.Outputs.Should().ContainSingle();
        result.Outputs[0].Result.Findings.Should().BeEmpty();
        result.AdapterFailures.Should().BeEmpty();
    }
}
