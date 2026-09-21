// Purpose: Aggregates consent-scoped community hardware observations for compatibility and performance analysis.
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;

namespace Dip.Application.Features.Intelligence;

public sealed class CommunityHardwareService
{
    private readonly ICommunityHardwareStore _store;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public CommunityHardwareService(
        ICommunityHardwareStore store,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _store = store;
        _uow = uow;
        _clock = clock;
    }

    public async Task SubmitAsync(
        CommunityHardwareSubmission submission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(submission.ConsentToken))
            throw new InvalidOperationException("Opt-in consent is required.");

        var hasClientPseudonym = !string.IsNullOrWhiteSpace(submission.ClientPseudonym);
        var fingerprint = CreatePseudonymousFingerprint(submission);

        if (hasClientPseudonym)
        {
            var existing = await _store.ListAsync(submission.HardwareKey, cancellationToken);
            // Only an explicit client pseudonym is used for retry deduplication.
            // ConsentToken is never treated as a device identity.
            if (existing.Any(sample =>
                    string.Equals(
                        sample.SourceFingerprint,
                        fingerprint,
                        StringComparison.OrdinalIgnoreCase)
                    && sample.MeasuredAtUtc >= _clock.UtcNow.AddHours(-24)))
            {
                return;
            }
        }

        var entity = new CommunityHardwareSample(
            Guid.NewGuid(),
            submission.HardwareKey,
            submission.Platform,
            submission.CpuArch,
            submission.GpuVendor,
            submission.OsFamily,
            submission.DriverFamily,
            submission.CpuSingleScore,
            submission.CpuMultiScore,
            submission.GpuScore,
            submission.MemoryScore,
            submission.StorageScore,
            submission.ThermalScore,
            submission.MemoryMb,
            fingerprint,
            _clock.UtcNow);

        await _store.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<CommunityHardwareAggregate> AggregateAsync(
        string hardwareKey,
        CancellationToken cancellationToken)
    {
        var samples = await _store.ListAsync(hardwareKey, cancellationToken);
        if (samples.Count == 0)
            return new(hardwareKey, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        static decimal Quantile(IEnumerable<decimal> values, double quantile)
        {
            var ordered = values.OrderBy(value => value).ToArray();
            var index = (int)Math.Clamp(
                Math.Round((ordered.Length - 1) * quantile),
                0,
                ordered.Length - 1);
            return ordered[index];
        }

        var n = samples.Count;
        var cpuMedian = Quantile(samples.Select(x => x.CpuMultiScore), 0.5);
        var cpuP10 = Quantile(samples.Select(x => x.CpuMultiScore), 0.1);
        var cpuP90 = Quantile(samples.Select(x => x.CpuMultiScore), 0.9);
        var gpuMedian = Quantile(samples.Select(x => x.GpuScore), 0.5);
        var gpuP10 = Quantile(samples.Select(x => x.GpuScore), 0.1);
        var gpuP90 = Quantile(samples.Select(x => x.GpuScore), 0.9);

        var fresh = samples.Count(x =>
            x.MeasuredAtUtc > _clock.UtcNow.AddDays(-180));
        var freshness = 100m * fresh / n;

        var sampleConfidence = n switch
        {
            < 5 => 25m,
            < 10 => 45m,
            < 50 => 70m,
            < 200 => 85m,
            _ => 95m
        };

        var driverDiversity = Math.Min(
            100m,
            samples.Select(x => x.DriverFamily ?? "unknown")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() * 20m);
        var osDiversity = Math.Min(
            100m,
            samples.Select(x => x.OsFamily)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() * 25m);

        static decimal DispersionPenalty(decimal p10, decimal median, decimal p90)
        {
            if (median <= 0m) return 20m;
            var spread = Math.Max(0m, p90 - p10) / median * 100m;
            return Math.Min(25m, spread * 0.25m);
        }

        var dispersionPenalty = (
            DispersionPenalty(cpuP10, cpuMedian, cpuP90)
            + DispersionPenalty(gpuP10, gpuMedian, gpuP90)) / 2m;

        var confidence = Math.Clamp(
            sampleConfidence * 0.55m
            + freshness * 0.25m
            + driverDiversity * 0.10m
            + osDiversity * 0.10m
            - dispersionPenalty,
            5m,
            99m);

        return new CommunityHardwareAggregate(
            hardwareKey,
            n,
            cpuMedian,
            cpuP10,
            cpuP90,
            gpuMedian,
            gpuP10,
            gpuP90,
            Quantile(samples.Select(x => x.MemoryScore), 0.5),
            Quantile(samples.Select(x => x.StorageScore), 0.5),
            Quantile(samples.Select(x => x.ThermalScore), 0.5),
            decimal.Round(confidence, 2));
    }

    private static string CreatePseudonymousFingerprint(
        CommunityHardwareSubmission submission)
    {
        var sourcePseudonym = string.IsNullOrWhiteSpace(submission.ClientPseudonym)
            ? Guid.NewGuid().ToString("N")
            : submission.ClientPseudonym.Trim();

        var raw = string.Join(
            '|',
            sourcePseudonym,
            submission.HardwareKey.Trim(),
            submission.Platform.Trim(),
            submission.CpuArch.Trim(),
            submission.GpuVendor.Trim(),
            submission.OsFamily.Trim(),
            submission.DriverFamily?.Trim() ?? string.Empty);

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
            .ToLowerInvariant();
    }
}
