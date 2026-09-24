// Purpose: Implements Visual Comparator responsibilities for visual qa.
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
        double sumA = 0, sumB = 0, sumAA = 0, sumBB = 0, sumAB = 0;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                ct.ThrowIfCancellationRequested();
                var index = (y * width + x) * 4;
                if (Masked(x, y, masks))
                {
                    diff[index] = 40; diff[index + 1] = 40; diff[index + 2] = 40; diff[index + 3] = 120;
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
                    diff[index] = 255; diff[index + 1] = 32; diff[index + 2] = 32; diff[index + 3] = 255;
                }
                else
                {
                    var luminance = (byte)Math.Clamp((int)Math.Round(0.2126 * b.Rgba[index] + 0.7152 * b.Rgba[index + 1] + 0.0722 * b.Rgba[index + 2]), 0, 255);
                    diff[index] = luminance; diff[index + 1] = luminance; diff[index + 2] = luminance; diff[index + 3] = 100;
                }

                var la = 0.2126 * a.Rgba[index] + 0.7152 * a.Rgba[index + 1] + 0.0722 * a.Rgba[index + 2];
                var lb = 0.2126 * b.Rgba[index] + 0.7152 * b.Rgba[index + 1] + 0.0722 * b.Rgba[index + 2];
                sumA += la; sumB += lb; sumAA += la * la; sumBB += lb * lb; sumAB += la * lb;
            }

        if (evaluated == 0) return new(false, width, height, 0, 0, 0m, 1m, null, "All pixels are masked.");
        var n = evaluated;
        var meanA = sumA / n; var meanB = sumB / n;
        var varA = Math.Max(0d, sumAA / n - meanA * meanA);
        var varB = Math.Max(0d, sumBB / n - meanB * meanB);
        var covariance = sumAB / n - meanA * meanB;
        var c1 = Math.Pow(0.01 * 255d, 2); var c2 = Math.Pow(0.03 * 255d, 2);
        var denominator = (meanA * meanA + meanB * meanB + c1) * (varA + varB + c2);
        var ssim = denominator <= double.Epsilon ? 1d : ((2 * meanA * meanB + c1) * (2 * covariance + c2)) / denominator;
        ssim = Math.Clamp(ssim, 0d, 1d);
        var percent = Math.Round(changed * 100m / evaluated, 4);
        return new(true, width, height, changed, evaluated, percent, Math.Round((decimal)ssim, 6), PortablePngCodec.EncodeRgba(width, height, diff), null);
    }

    private static bool Masked(int x, int y, IReadOnlyList<VisualMaskRegion> masks)
        => masks.Any(mask => mask.Width > 0 && mask.Height > 0
            && x >= mask.X && y >= mask.Y && x < mask.X + mask.Width && y < mask.Y + mask.Height);
}
