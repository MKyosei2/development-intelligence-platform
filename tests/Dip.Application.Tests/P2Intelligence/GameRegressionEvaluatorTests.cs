// Purpose: Defines Game Regression Evaluator Tests responsibilities for this repository area.
using Dip.Application.Features.P2Intelligence;
using FluentAssertions;

namespace Dip.Application.Tests.P2Intelligence;

public sealed class GameRegressionEvaluatorTests
{
    [Fact]
    public void Evaluate_FailsWhenFrameBudgetAndFpsTargetAreMissed()
    {
        var sut = new GameRegressionEvaluator();
        var result = sut.Evaluate(new GameRegressionCommand(
            Guid.NewGuid(), "game",
            [new("fps", 60m, "fps"), new("frame_time_ms", 16m, "ms"), new("gpu_ms", 8m, "ms")],
            [new("fps", 45m, "fps"), new("frame_time_ms", 22m, "ms"), new("gpu_ms", 10m, "ms")],
            TargetFps: 60));

        result.Status.Should().Be("Failed");
        result.Blockers.Should().NotBeEmpty();
        result.Metrics.Should().Contain(x => x.Name == "fps" && x.Status == "Failed");
        result.Metrics.Should().Contain(x => x.Name == "frame_time_ms" && x.Status == "Failed");
    }

    [Fact]
    public void Evaluate_PassesStableMetrics()
    {
        var sut = new GameRegressionEvaluator();
        var result = sut.Evaluate(new GameRegressionCommand(
            Guid.NewGuid(), "game",
            [new("fps", 60m, "fps"), new("frame_time_ms", 15m, "ms"), new("memory_mb", 1000m, "MB")],
            [new("fps", 62m, "fps"), new("frame_time_ms", 15.3m, "ms"), new("memory_mb", 1020m, "MB")],
            TargetFps: 60));

        result.Status.Should().Be("Passed");
        result.Blockers.Should().BeEmpty();
    }
    [Fact]
    public void Evaluate_FlagsOnlineNetworkEnvelopeViolations()
    {
        var sut = new GameRegressionEvaluator();
        var result = sut.Evaluate(new GameRegressionCommand(
            Guid.NewGuid(), "online",
            [new("packet_loss_percent", 0.5m, "%"), new("rtt_ms", 50m, "ms"), new("jitter_ms", 10m, "ms")],
            [new("packet_loss_percent", 6m, "%"), new("rtt_ms", 280m, "ms"), new("jitter_ms", 75m, "ms")],
            TargetFps: 60));

        result.Status.Should().Be("Failed");
        result.Blockers.Should().HaveCountGreaterThanOrEqualTo(3);
    }

}
