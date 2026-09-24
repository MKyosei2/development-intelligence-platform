// Purpose: Implements P1 Re po si to ri es infrastructure integration for p er si st en ce.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class QaRunRepository(DipDbContext db) : IQaRunRepository
{
    public Task AddAsync(QaRun run, CancellationToken ct) => db.QaRuns.AddAsync(run, ct).AsTask();
    public Task<QaRun?> GetAsync(Guid id, CancellationToken ct) => db.QaRuns.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<QaRun>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) => await db.QaRuns.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
}
public sealed class QaFindingRepository(DipDbContext db) : IQaFindingRepository
{
    public Task AddRangeAsync(IEnumerable<QaFinding> findings, CancellationToken ct) { db.QaFindings.AddRange(findings); return Task.CompletedTask; }
    public Task<QaFinding?> GetAsync(Guid id, CancellationToken ct) => db.QaFindings.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<QaFinding>> ListByRunAsync(Guid runId, CancellationToken ct) => await db.QaFindings.AsNoTracking().Where(x => x.QaRunId == runId).OrderByDescending(x => x.Severity).ThenBy(x => x.Title).ToListAsync(ct);
    public async Task<IReadOnlyList<QaFinding>> ListRecentByProjectAsync(Guid projectId, int take, CancellationToken ct) =>
        await db.QaFindings.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.ObservedAtUtc).Take(Math.Clamp(take, 1, 10000)).ToListAsync(ct);
}
public sealed class ReproductionAttemptRepository(DipDbContext db) : IReproductionAttemptRepository
{
    public Task AddAsync(ReproductionAttempt attempt, CancellationToken ct) => db.ReproductionAttempts.AddAsync(attempt, ct).AsTask();
    public async Task<IReadOnlyList<ReproductionAttempt>> ListByFindingAsync(Guid findingId, CancellationToken ct) => await db.ReproductionAttempts.AsNoTracking().Where(x => x.QaFindingId == findingId).OrderBy(x => x.AttemptNumber).ToListAsync(ct);
}
public sealed class HardwareProfileRepository(DipDbContext db) : IHardwareProfileRepository
{
    public Task AddAsync(HardwareProfile profile, CancellationToken ct) => db.HardwareProfiles.AddAsync(profile, ct).AsTask();
    public Task<HardwareProfile?> GetAsync(Guid id, CancellationToken ct) => db.HardwareProfiles.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<HardwareProfile?> FindAsync(string name, string platform, CancellationToken ct) => db.HardwareProfiles.SingleOrDefaultAsync(x => x.Name == name && x.Platform == platform, ct);
    public async Task<IReadOnlyList<HardwareProfile>> ListAsync(CancellationToken ct) => await db.HardwareProfiles.AsNoTracking().OrderBy(x => x.Platform).ThenBy(x => x.Name).ToListAsync(ct);
}
public sealed class HardwareCalibrationRepository(DipDbContext db) : IHardwareCalibrationRepository
{
    public Task AddAsync(HardwareCalibration calibration, CancellationToken ct) => db.HardwareCalibrations.AddAsync(calibration, ct).AsTask();
    public Task<HardwareCalibration?> GetLatestAsync(Guid projectId, CancellationToken ct) => db.HardwareCalibrations.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.MeasuredAtUtc).FirstOrDefaultAsync(ct);
}
public sealed class PerformanceSimulationRepository(DipDbContext db) : IPerformanceSimulationRepository
{
    public Task AddAsync(PerformanceSimulationRun run, CancellationToken ct) => db.PerformanceSimulationRuns.AddAsync(run, ct).AsTask();
    public Task<PerformanceSimulationRun?> GetAsync(Guid id, CancellationToken ct) => db.PerformanceSimulationRuns.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<PerformanceSimulationRun>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) => await db.PerformanceSimulationRuns.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
}
