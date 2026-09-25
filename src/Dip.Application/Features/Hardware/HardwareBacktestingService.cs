// Purpose: Coordinates Hardware Backtesting Service application behavior for Hardware.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;

namespace Dip.Application.Features.Hardware;

public sealed record RecordHardwareBacktestCommand(Guid ProjectId, Guid HardwareProfileId, string ScenarioKey, string CommitSha, decimal PredictedFps, decimal ActualFps, string EnvironmentFingerprint);
public sealed record HardwareBacktestDto(Guid Id, Guid HardwareProfileId, string ScenarioKey, string CommitSha, decimal PredictedFps, decimal ActualFps, decimal ErrorPercent, string EnvironmentFingerprint, DateTimeOffset MeasuredAtUtc);
public sealed record HardwareBacktestSummary(int Samples, decimal MeanErrorPercent, decimal MeanAbsoluteErrorPercent, decimal RootMeanSquareErrorPercent, decimal CalibrationFactor, decimal ConfidencePercent);

public sealed class HardwareBacktestingService
{
    private readonly IHardwareBacktestRepository _repo;
     private readonly IHardwareProfileRepository _profiles;
     private readonly IUnitOfWork _uow;
     private readonly ISystemClock _clock;
    public HardwareBacktestingService(IHardwareBacktestRepository repo, IHardwareProfileRepository profiles, IUnitOfWork uow, ISystemClock clock) { _repo = repo;
     _profiles = profiles;
     _uow = uow;
     _clock = clock;
     }
    public async Task<HardwareBacktestDto> RecordAsync(RecordHardwareBacktestCommand command, CancellationToken ct) { if (await _profiles.GetAsync(command.HardwareProfileId, ct) is null) throw new KeyNotFoundException($"Hardware profile {command.HardwareProfileId} was not found.");
    if (command.PredictedFps < 0 || command.ActualFps < 0) throw new ArgumentOutOfRangeException(nameof(command), "FPS values cannot be negative.");
     var entity = new HardwareBacktest(Guid.NewGuid(), command.ProjectId, command.HardwareProfileId, command.ScenarioKey, command.CommitSha, command.PredictedFps, command.ActualFps, command.EnvironmentFingerprint, _clock.UtcNow);
     await _repo.AddAsync(entity, ct);
     await _uow.SaveChangesAsync(ct);
     return Map(entity);
     }
    public async Task<IReadOnlyList<HardwareBacktestDto>> ListAsync(Guid projectId, Guid? profileId, int take, CancellationToken ct) => (await _repo.ListAsync(projectId, profileId, take, ct)).Select(Map).ToArray();
    public async Task<HardwareBacktestSummary> SummarizeAsync(Guid projectId, Guid? profileId, int take, CancellationToken ct) { var rows = await _repo.ListAsync(projectId, profileId, take, ct);
    if (rows.Count == 0) return new(0, 0, 0, 0, 1m, 0);
     var errors = rows.Select(x => x.PredictionErrorPercent).ToArray();
     var mean = errors.Average();
     var mae = errors.Select(x => Math.Abs(x)).Average();
     var rmse = (decimal)Math.Sqrt((double)errors.Select(x => x * x).Average());
     var factor = rows.Where(x => x.PredictedFps > 0).Select(x => x.ActualFps / x.PredictedFps).DefaultIfEmpty(1m).Average();
     var confidence = Math.Clamp(100m - mae * 2m, 0m, 100m);
     return new(rows.Count, decimal.Round(mean, 3), decimal.Round(mae, 3), decimal.Round(rmse, 3), decimal.Round(factor, 4), decimal.Round(confidence, 2));
     }
    private static HardwareBacktestDto Map(HardwareBacktest x) => new(x.Id, x.HardwareProfileId, x.ScenarioKey, x.CommitSha, x.PredictedFps, x.ActualFps, x.PredictionErrorPercent, x.EnvironmentFingerprint, x.MeasuredAtUtc);
}
