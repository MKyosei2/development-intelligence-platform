// Purpose: Implements deterministic PNG visual comparison shared by durable Visual QA and autonomous media regression.
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public interface IVisualComparator
{
    Task<VisualComparisonComputation> CompareAsync(
        Stream baseline,
        Stream current,
        IReadOnlyList<VisualMaskRegion> masks,
        CancellationToken ct);
}

public sealed class PortablePngVisualComparator : IVisualComparator
{
    private const int ChannelTolerance = 8;
    private const int SsimTileSize = 8;

    public async Task<VisualComparisonComputation> CompareAsync(
        Stream baseline,
        Stream current,
        IReadOnlyList<VisualMaskRegion> masks,
        CancellationToken ct)
    {
        PortablePngImage a;
        PortablePngImage b;
        try
        {
            a = await PortablePngCodec.DecodeAsync(baseline, ct);
            b = await PortablePngCodec.DecodeAsync(current, ct);
        }
        catch (InvalidDataException ex)
        {
            return new(false, 0, 0, 0, 0, 100m, 0m, null, ex.Message);
        }

        if (a.Width != b.Width || a.Height != b.Height)
            return new(false, b.Width, b.Height, 0, 0, 100m, 0m, null,
                $"Image dimensions differ: baseline={a.Width}x{a.Height}, current={b.Width}x{b.Height}.");

        var width = a.Width;
        var height = a.Height;
        var diff = new byte[a.Rgba.Length];
        var changed = 0;
        var evaluated = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                ct.ThrowIfCancellationRequested();
                var index = (y * width + x) * 4;
                if (Masked(x, y, masks))
                {
                    diff[index] = 40;
                    diff[index + 1] = 40;
                    diff[index + 2] = 40;
                    diff[index + 3] = 120;
                    continue;
                }

                evaluated++;
                var dr = Math.Abs(a.Rgba[index] - b.Rgba[index]);
                var dg = Math.Abs(a.Rgba[index + 1] - b.Rgba[index + 1]);
                var db = Math.Abs(a.Rgba[index + 2] - b.Rgba[index + 2]);
                var isChanged = Math.Max(dr, Math.Max(dg, db)) > ChannelTolerance;
                if (isChanged)
                {
                    changed++;
                    diff[index] = 255;
                    diff[index + 1] = 32;
                    diff[index + 2] = 32;
                    diff[index + 3] = 255;
                }
                else
                {
                    var luminance = Luminance(b.Rgba[index], b.Rgba[index + 1], b.Rgba[index + 2]);
                    var gray = (byte)Math.Clamp((int)Math.Round(luminance), 0, 255);
                    diff[index] = gray;
                    diff[index + 1] = gray;
                    diff[index + 2] = gray;
                    diff[index + 3] = 100;
                }
            }
        }

        if (evaluated == 0)
            return new(false, width, height, 0, 0, 0m, 1m, null, "All pixels are masked.");

        var ssim = ComputeTiledSsim(a, b, masks, ct);
        var percent = Math.Round(changed * 100m / evaluated, 4);
        return new(
            true,
            width,
            height,
            changed,
            evaluated,
            percent,
            Math.Round(ssim, 6),
            PortablePngCodec.EncodeRgba(width, height, diff),
            null);
    }

    private static decimal ComputeTiledSsim(
        PortablePngImage a,
        PortablePngImage b,
        IReadOnlyList<VisualMaskRegion> masks,
        CancellationToken ct)
    {
        decimal total = 0m;
        var tiles = 0;
        for (var originY = 0; originY < a.Height; originY += SsimTileSize)
        {
            for (var originX = 0; originX < a.Width; originX += SsimTileSize)
            {
                ct.ThrowIfCancellationRequested();
                double sumA = 0d, sumB = 0d, sumAA = 0d, sumBB = 0d, sumAB = 0d;
                var count = 0;
                var maxY = Math.Min(a.Height, originY + SsimTileSize);
                var maxX = Math.Min(a.Width, originX + SsimTileSize);
                for (var y = originY; y < maxY; y++)
                {
                    for (var x = originX; x < maxX; x++)
                    {
                        if (Masked(x, y, masks))
                            continue;
                        var index = (y * a.Width + x) * 4;
                        var la = Luminance(a.Rgba[index], a.Rgba[index + 1], a.Rgba[index + 2]);
                        var lb = Luminance(b.Rgba[index], b.Rgba[index + 1], b.Rgba[index + 2]);
                        sumA += la;
                        sumB += lb;
                        sumAA += la * la;
                        sumBB += lb * lb;
                        sumAB += la * lb;
                        count++;
                    }
                }

                if (count == 0)
                    continue;

                var meanA = sumA / count;
                var meanB = sumB / count;
                var varianceA = Math.Max(0d, sumAA / count - meanA * meanA);
                var varianceB = Math.Max(0d, sumBB / count - meanB * meanB);
                var covariance = sumAB / count - meanA * meanB;
                var c1 = Math.Pow(0.01 * 255d, 2);
                var c2 = Math.Pow(0.03 * 255d, 2);
                var denominator = (meanA * meanA + meanB * meanB + c1)
                                  * (varianceA + varianceB + c2);
                var value = denominator <= double.Epsilon
                    ? 1d
                    : ((2d * meanA * meanB + c1) * (2d * covariance + c2)) / denominator;
                total += (decimal)Math.Clamp(value, 0d, 1d);
                tiles++;
            }
        }

        return tiles == 0 ? 1m : total / tiles;
    }

    private static double Luminance(byte r, byte g, byte b)
        => 0.2126d * r + 0.7152d * g + 0.0722d * b;

    private static bool Masked(int x, int y, IReadOnlyList<VisualMaskRegion> masks)
        => masks.Any(mask => mask.Width > 0 && mask.Height > 0
            && x >= mask.X && y >= mask.Y && x < mask.X + mask.Width && y < mask.Y + mask.Height);
}
