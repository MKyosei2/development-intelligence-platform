using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Hardware;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Intelligence;

public sealed class P2OrchestrationService
{
    private readonly IIntelligenceRunStore _runs;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthService _truth;
    private readonly IMinimumHardwareRunner _minimum;
    private readonly IRegressionRunner _regression;
    private readonly IReliabilityRunner _reliability;
    private readonly IMediaRegressionRunner _media;
    private readonly IFuzzRunner _fuzz;
    private readonly IMutationRunner _mutation;
    private readonly HardwareBacktestingService _backtests;

    public P2OrchestrationService(
        IIntelligenceRunStore runs,
        IEvidenceRepository evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        ProjectTruthService truth,
        IMinimumHardwareRunner minimum,
        IRegressionRunner regression,
        IReliabilityRunner reliability,
        IMediaRegressionRunner media,
        IFuzzRunner fuzz,
        IMutationRunner mutation,
        HardwareBacktestingService backtests)
    {
        _runs = runs;
        _evidence = evidence;
        _uow = uow;
        _clock = clock;
        _truth = truth;
        _minimum = minimum;
        _regression = regression;
        _reliability = reliability;
        _media = media;
        _fuzz = fuzz;
        _mutation = mutation;
        _backtests = backtests;
    }

    private async Task<(IntelligenceRun Run, string Directory)> BeginAsync(
        Guid projectId,
        string subject,
        IntelligenceRunType type,
        object input,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        var id = Guid.NewGuid();
        var directory = Path.Combine(
            Path.GetFullPath("artifacts/p2"),
            projectId.ToString("N"),
            id.ToString("N"));
        Directory.CreateDirectory(directory);

        var run = new IntelligenceRun(
            id,
            projectId,
            type,
            subject,
            directory,
            JsonSerializer.Serialize(input),
            _clock.UtcNow);
        await _runs.AddAsync(run, cancellationToken);
        run.Start(_clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return (run, directory);
    }

    private async Task FinishAsync(
        IntelligenceRun run,
        IntelligenceRunStatus status,
        object result,
        EvidenceRecord evidence,
        CancellationToken cancellationToken)
    {
        run.Complete(
            status,
            Trim(JsonSerializer.Serialize(result), 32000),
            _clock.UtcNow);
        await _evidence.AddAsync(evidence, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        await _truth.RecalculateAsync(run.ProjectId, cancellationToken);
    }

    private async Task FailInfrastructureAsync(
        IntelligenceRun run,
        Exception exception,
        CancellationToken cancellationToken)
    {
        run.Complete(
            IntelligenceRunStatus.InfrastructureFailure,
            Trim(
                JsonSerializer.Serialize(new
                {
                    error = exception.Message,
                    type = exception.GetType().FullName
                }),
                32000),
            _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<MinimumHardwareResult> MinimumHardwareAsync(
        MinimumHardwareRequest request,
        CancellationToken cancellationToken)
    {
        var (run, directory) = await BeginAsync(
            request.ProjectId,
            request.SubjectKey,
            IntelligenceRunType.MinimumHardware,
            request,
            cancellationToken);
        try
        {
            var rawResult = await _minimum.FindAsync(request, directory, cancellationToken);
            var calibration = await _backtests.SummarizeAsync(request.ProjectId, null, 1000, cancellationToken);
            var result = rawResult;
            if (calibration.Samples >= 3 && calibration.CalibrationFactor > 0m)
            {
                var correction = Math.Clamp(1m / calibration.CalibrationFactor, 0.75m, 1.50m);
                decimal Correct(decimal value) => Math.Min(100m, decimal.Round(value * correction, 2));
                var confidence = decimal.Round(Math.Clamp(rawResult.ConfidencePercent * .6m + calibration.ConfidencePercent * .4m, 0m, 95m), 2);
                result = rawResult with
                {
                    SafeCpuPercent = Correct(rawResult.SafeCpuPercent),
                    SafeGpuPercent = Correct(rawResult.SafeGpuPercent),
                    SafeMemoryPercent = Correct(rawResult.SafeMemoryPercent),
                    SafeStoragePercent = Correct(rawResult.SafeStoragePercent),
                    ConfidencePercent = confidence,
                    EvidenceJson = JsonSerializer.Serialize(new { simulation = rawResult.EvidenceJson, realDeviceBacktest = calibration, correctionFactor = correction }, new JsonSerializerOptions { WriteIndented = true })
                };
            }
            var confidence = result.ConfidencePercent >= 60m
                ? EvidenceConfidence.Medium
                : result.ConfidencePercent >= 35m
                    ? EvidenceConfidence.Low
                    : EvidenceConfidence.Unverified;

            var evidence = new EvidenceRecord(
                Guid.NewGuid(),
                request.ProjectId,
                null,
                request.SubjectKey,
                EvidenceSourceType.Performance,
                "hardware.minimum-estimate",
                EvidenceOutcome.Informational,
                "Estimated minimum hardware boundary",
                $"Estimated safe CPU {result.SafeCpuPercent:0.#}% / GPU {result.SafeGpuPercent:0.#}% of the current QA machine under constrained simulation. This is simulation-derived evidence, not a real-device measurement.",
                confidence,
                null,
                Trim(result.EvidenceJson, 16000),
                _clock.UtcNow,
                _clock.UtcNow);
            await FinishAsync(
                run,
                IntelligenceRunStatus.Succeeded,
                result,
                evidence,
                cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, exception, cancellationToken);
            throw;
        }
    }

    public async Task<RegressionResult> RegressionAsync(
        RegressionRequest request,
        CancellationToken cancellationToken)
    {
        var (run, directory) = await BeginAsync(
            request.ProjectId,
            request.SubjectKey,
            IntelligenceRunType.GitRegression,
            request,
            cancellationToken);
        try
        {
            var result = await _regression.FindOriginAsync(
                request,
                directory,
                cancellationToken);
            var resolved = result.FirstBadCommit is not null;
            var evidence = new EvidenceRecord(
                Guid.NewGuid(),
                request.ProjectId,
                null,
                request.SubjectKey,
                EvidenceSourceType.GitHub,
                "regression.origin",
                resolved ? EvidenceOutcome.Failed : EvidenceOutcome.Unknown,
                resolved ? "Regression origin isolated" : "Regression origin not resolved",
                resolved
                    ? $"First bad commit: {result.FirstBadCommit}"
                    : "Automated commit search could not isolate a first bad commit.",
                resolved ? EvidenceConfidence.High : EvidenceConfidence.Medium,
                result.FirstBadCommit,
                Trim(result.EvidenceJson, 16000),
                _clock.UtcNow,
                _clock.UtcNow);
            await FinishAsync(
                run,
                resolved ? IntelligenceRunStatus.Succeeded : IntelligenceRunStatus.Warning,
                result,
                evidence,
                cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, exception, cancellationToken);
            throw;
        }
    }

    public async Task<ReliabilityResult> ReliabilityAsync(
        ReliabilityRequest request,
        CancellationToken cancellationToken)
    {
        var type = request.SoakMinutes > 0
            ? IntelligenceRunType.Soak
            : IntelligenceRunType.Flaky;
        var (run, directory) = await BeginAsync(
            request.ProjectId,
            request.SubjectKey,
            type,
            request,
            cancellationToken);
        try
        {
            var result = await _reliability.RunAsync(
                request,
                directory,
                cancellationToken);
            var risky = result.FailureRate > 5m
                || result.PerformanceDriftPercent > 15m
                || result.MemoryGrowthMb > 256m;
            var evidence = new EvidenceRecord(
                Guid.NewGuid(),
                request.ProjectId,
                null,
                request.SubjectKey,
                EvidenceSourceType.Runtime,
                request.SoakMinutes > 0 ? "runtime.soak" : "runtime.flaky",
                risky ? EvidenceOutcome.Failed : EvidenceOutcome.Succeeded,
                risky ? "Reliability risk detected" : "Reliability run passed",
                $"Failure rate {result.FailureRate:0.##}%, memory growth {result.MemoryGrowthMb:0.#} MB, performance drift {result.PerformanceDriftPercent:0.#}%.",
                EvidenceConfidence.High,
                null,
                Trim(result.EvidenceJson, 16000),
                _clock.UtcNow,
                _clock.UtcNow);
            await FinishAsync(
                run,
                risky ? IntelligenceRunStatus.Warning : IntelligenceRunStatus.Succeeded,
                result,
                evidence,
                cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, exception, cancellationToken);
            throw;
        }
    }

    public async Task<MediaRegressionResult> MediaAsync(
        VisualRegressionRequest request,
        string kind,
        CancellationToken cancellationToken)
    {
        var normalized = kind.ToLowerInvariant();
        var type = normalized switch
        {
            "video" => IntelligenceRunType.VideoRegression,
            "audio" => IntelligenceRunType.AudioRegression,
            _ => IntelligenceRunType.VisualRegression
        };
        var (run, directory) = await BeginAsync(
            request.ProjectId,
            request.SubjectKey,
            type,
            request,
            cancellationToken);
        try
        {
            var result = await _media.CompareAsync(
                request,
                directory,
                normalized,
                cancellationToken);
            var evidenceConfidence = MediaEvidenceConfidence(result.EvidenceJson);
            var evidence = new EvidenceRecord(
                Guid.NewGuid(),
                request.ProjectId,
                null,
                request.SubjectKey,
                EvidenceSourceType.Runtime,
                $"regression.{normalized}",
                result.Passed ? EvidenceOutcome.Succeeded : EvidenceOutcome.Failed,
                $"{normalized} regression {(result.Passed ? "passed" : "detected")}",
                $"Difference {result.DifferencePercent:0.###}% against the supplied baseline.",
                evidenceConfidence,
                result.DiffArtifact,
                Trim(result.EvidenceJson, 16000),
                _clock.UtcNow,
                _clock.UtcNow);
            await FinishAsync(
                run,
                result.Passed ? IntelligenceRunStatus.Succeeded : IntelligenceRunStatus.Warning,
                result,
                evidence,
                cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, exception, cancellationToken);
            throw;
        }
    }

    public async Task<FuzzResult> FuzzAsync(
        FuzzRequest request,
        CancellationToken cancellationToken)
    {
        var (run, directory) = await BeginAsync(
            request.ProjectId,
            request.SubjectKey,
            IntelligenceRunType.Fuzz,
            request,
            cancellationToken);
        try
        {
            var result = await _fuzz.RunAsync(request, directory, cancellationToken);
            var risky = result.Crashes > 0 || result.Timeouts > 0;
            var evidence = new EvidenceRecord(
                Guid.NewGuid(),
                request.ProjectId,
                null,
                request.SubjectKey,
                EvidenceSourceType.Quality,
                "qa.fuzz",
                risky ? EvidenceOutcome.Failed : EvidenceOutcome.Succeeded,
                risky ? "Fuzzing found unstable input behavior" : "Fuzz campaign completed",
                $"{result.Cases} cases, {result.Crashes} crash-like exit(s), {result.Timeouts} timeout(s), {result.NonZeroExits} non-zero exit(s).",
                EvidenceConfidence.High,
                result.ArtifactPath,
                Trim(result.EvidenceJson, 16000),
                _clock.UtcNow,
                _clock.UtcNow);
            await FinishAsync(
                run,
                risky ? IntelligenceRunStatus.Warning : IntelligenceRunStatus.Succeeded,
                result,
                evidence,
                cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, exception, cancellationToken);
            throw;
        }
    }

    public async Task<MutationResult> MutationAsync(
        MutationRequest request,
        CancellationToken cancellationToken)
    {
        var (run, directory) = await BeginAsync(
            request.ProjectId,
            request.SubjectKey,
            IntelligenceRunType.Mutation,
            request,
            cancellationToken);
        try
        {
            var result = await _mutation.RunAsync(
                request,
                directory,
                cancellationToken);
            var weak = result.MutationScore < 70m;
            var evidence = new EvidenceRecord(
                Guid.NewGuid(),
                request.ProjectId,
                null,
                request.SubjectKey,
                EvidenceSourceType.Test,
                "qa.mutation",
                weak ? EvidenceOutcome.Warning : EvidenceOutcome.Succeeded,
                weak ? "Test strength appears weak" : "Mutation test strength passed",
                $"Mutation score {result.MutationScore:0.##}% ({result.Killed}/{result.Mutants} mutants killed).",
                EvidenceConfidence.High,
                null,
                Trim(result.EvidenceJson, 16000),
                _clock.UtcNow,
                _clock.UtcNow);
            await FinishAsync(
                run,
                weak ? IntelligenceRunStatus.Warning : IntelligenceRunStatus.Succeeded,
                result,
                evidence,
                cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, exception, cancellationToken);
            throw;
        }
    }

    private static EvidenceConfidence MediaEvidenceConfidence(string evidenceJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(evidenceJson);
            if (doc.RootElement.TryGetProperty("comparison", out var comparison)
                && comparison.TryGetProperty("precision", out var precision)
                && precision.ValueKind == JsonValueKind.String)
            {
                return string.Equals(
                    precision.GetString(),
                    "low",
                    StringComparison.OrdinalIgnoreCase)
                    ? EvidenceConfidence.Low
                    : EvidenceConfidence.High;
            }

            return EvidenceConfidence.High;
        }
        catch (JsonException)
        {
            return EvidenceConfidence.Low;
        }
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
