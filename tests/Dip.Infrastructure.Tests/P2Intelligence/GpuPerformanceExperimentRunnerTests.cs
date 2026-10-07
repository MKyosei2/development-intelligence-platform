// Purpose: Verifies the engine-to-DIP GPU telemetry contract rejects malformed data and preserves valid performance counters.
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class GpuPerformanceExperimentRunnerTests
{
    [Fact]
    public void ParseTelemetry_ParsesEnginePerformanceCounters()
    {
        var samples = GpuPerformanceExperimentRunner.ParseTelemetry(
            "DIP_GPU_METRIC frame=42 gpu_frame_ms=12.5 vram_mb=2048 draw_calls=321 triangles=450000 shader_variants=88");

        samples.Should().ContainSingle();
        samples[0].Sequence.Should().Be(42);
        samples[0].GpuFrameMs.Should().Be(12.5m);
        samples[0].VramMb.Should().Be(2048);
        samples[0].DrawCalls.Should().Be(321);
        samples[0].Triangles.Should().Be(450000);
        samples[0].ShaderVariants.Should().Be(88);
    }

    [Fact]
    public void ParseTelemetry_RejectsNegativeOrMissingGpuFrameTime()
    {
        var samples = GpuPerformanceExperimentRunner.ParseTelemetry(string.Join('\n', new[]
        {
            "DIP_GPU_METRIC frame=1 gpu_frame_ms=-1 vram_mb=100",
            "DIP_GPU_METRIC frame=2 vram_mb=100",
            "noise frame=3 gpu_frame_ms=10"
        }));

        samples.Should().BeEmpty();
    }
}
