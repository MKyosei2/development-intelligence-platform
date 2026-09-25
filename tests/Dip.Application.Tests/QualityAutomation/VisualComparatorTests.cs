// Purpose: Defines Visual Comparator Tests responsibilities for this repository area.
using Dip.Application.Features.QualityAutomation.VisualQa;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class VisualComparatorTests
{
    [Fact]
    public async Task Identical_png_is_a_clean_match()
    {
        var rgba = new byte[]
        {
            10,20,30,255, 40,50,60,255,
            70,80,90,255, 100,110,120,255
        };
        var png = PortablePngCodec.EncodeRgba(2, 2, rgba);
        var comparator = new PortablePngVisualComparator();
        await using var a = new MemoryStream(png);
        await using var b = new MemoryStream(png);
        var result = await comparator.CompareAsync(a, b, Array.Empty<VisualMaskRegion>(), CancellationToken.None);
        result.Compatible.Should().BeTrue();
        result.ChangedPixelCount.Should().Be(0);
        result.ChangedPixelPercent.Should().Be(0m);
        result.Ssim.Should().Be(1m);
        result.DiffPng.Should().NotBeNull();
        result.DiffPng!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Changed_pixel_is_detected_and_mask_can_exclude_it()
    {
        var baseline = PortablePngCodec.EncodeRgba(2, 1, new byte[] { 0, 0, 0, 255, 0, 0, 0, 255 });
        var current = PortablePngCodec.EncodeRgba(2, 1, new byte[] { 255, 255, 255, 255, 0, 0, 0, 255 });
        var comparator = new PortablePngVisualComparator();
        await using var a = new MemoryStream(baseline);
        await using var b = new MemoryStream(current);
        var changed = await comparator.CompareAsync(a, b, Array.Empty<VisualMaskRegion>(), CancellationToken.None);
        changed.ChangedPixelCount.Should().Be(1);
        changed.ChangedPixelPercent.Should().Be(50m);

        await using var a2 = new MemoryStream(baseline);
        await using var b2 = new MemoryStream(current);
        var masked = await comparator.CompareAsync(a2, b2, new[] { new VisualMaskRegion(0, 0, 1, 1) }, CancellationToken.None);
        masked.ChangedPixelCount.Should().Be(0);
        masked.ChangedPixelPercent.Should().Be(0m);
    }
}
