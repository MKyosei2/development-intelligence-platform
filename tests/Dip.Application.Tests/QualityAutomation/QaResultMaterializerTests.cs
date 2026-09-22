// Purpose: Verifies run materialization derives a successful final status from available adapter evidence.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaResultMaterializerTests
{
    [Fact]
    public async Task Complete_WithAvailableAdapterAndNoFindings_CompletesSucceeded()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-materialize-").FullName;
        await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
        var run = QaSplitTestFixtures.CreateRun(workspace);
        run.Start(DateTimeOffset.UtcNow);

        var output = await plan.Selections[0].Adapter.RunAsync(
            plan.Command, workspace, workspace, CancellationToken.None);
        var batch = new QaAdapterExecutionBatch(
            [(plan.Selections[0].Language, output)],
            [],
            []);
        var sut = new QaResultMaterializer(null!, null!, null!, new FixedClock(DateTimeOffset.UtcNow));

        var result = sut.Complete(plan, batch, run, Array.Empty<QaFinding>(), 0, 0);

        run.Status.Should().Be(QaRunStatus.Succeeded);
        result.Summary.Should().Contain("\"schema\":\"dip-universal-qa-v2\"");
    }

    private sealed class FixedClock(DateTimeOffset now) : Dip.Application.Abstractions.Time.ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
