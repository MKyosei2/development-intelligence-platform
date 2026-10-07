// Purpose: Verifies multi-client telemetry parsing, desync detection, and fail-closed network-impairment acknowledgement.
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class MultiplayerQaExperimentRunnerTests
{
    [Fact]
    public void EvaluateTelemetry_PassesSynchronizedClients()
    {
        var samples = BuildSamples("same", "same", impairment: true);

        var result = MultiplayerQaExperimentRunner.EvaluateTelemetry(samples, 2, 2, 0, impairmentRequested: true);

        result.Status.Should().Be("Passed");
        result.CommonTicks.Should().Be(2);
        result.DesyncTicks.Should().Be(0);
        result.NetworkImpairmentVerified.Should().BeTrue();
    }

    [Fact]
    public void EvaluateTelemetry_FailsOnDesync()
    {
        var samples = BuildSamples("client-a", "client-b", impairment: true);

        var result = MultiplayerQaExperimentRunner.EvaluateTelemetry(samples, 2, 2, 0, impairmentRequested: true);

        result.Status.Should().Be("Failed");
        result.DesyncTicks.Should().Be(2);
    }

    [Fact]
    public void EvaluateTelemetry_FailsClosedWhenImpairmentIsNotAcknowledged()
    {
        var samples = BuildSamples("same", "same", impairment: false);

        var result = MultiplayerQaExperimentRunner.EvaluateTelemetry(samples, 2, 2, 0, impairmentRequested: true);

        result.Status.Should().Be("InfrastructureFailure");
        result.NetworkImpairmentVerified.Should().BeFalse();
    }

    [Fact]
    public void ParseTelemetry_RejectsMissingStateHash()
    {
        MultiplayerQaExperimentRunner.ParseTelemetry(
            "DIP_MP_STATE client=0 tick=1 rtt_ms=10 network_impairment_applied=1")
            .Should().BeEmpty();
    }

    private static IReadOnlyList<MultiplayerStateSample> BuildSamples(string hash0, string hash1, bool impairment)
        => new[]
        {
            new MultiplayerStateSample(0, 1, hash0, 10m, 1m, impairment),
            new MultiplayerStateSample(1, 1, hash1, 11m, 1m, impairment),
            new MultiplayerStateSample(0, 2, hash0, 10m, 1m, impairment),
            new MultiplayerStateSample(1, 2, hash1, 11m, 1m, impairment)
        };
}
