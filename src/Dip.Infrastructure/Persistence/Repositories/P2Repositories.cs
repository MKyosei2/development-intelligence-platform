// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class P2IntelligenceRepository(DipDbContext db) : IP2IntelligenceRepository
{
    public Task AddAsync(P2IntelligenceRecord record, CancellationToken ct) => db.P2IntelligenceRecords.AddAsync(record, ct).AsTask();

    public async Task<IReadOnlyList<P2IntelligenceRecord>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) =>
        await db.P2IntelligenceRecords.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<P2IntelligenceRecord>> ListByKindAsync(Guid projectId, P2IntelligenceKind kind, int take, CancellationToken ct) =>
        await db.P2IntelligenceRecords.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Kind == kind)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
}

public sealed class HardwareCommunityRepository(DipDbContext db) : IHardwareCommunityRepository
{
    public Task AddAsync(HardwareCommunitySample sample, CancellationToken ct) => db.HardwareCommunitySamples.AddAsync(sample, ct).AsTask();

    public async Task<IReadOnlyList<HardwareCommunitySample>> ListAsync(string profileKey, string platform, int take, CancellationToken ct) =>
        await db.HardwareCommunitySamples.AsNoTracking()
            .Where(x => x.ProfileKey == profileKey && x.Platform == platform)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Take(Math.Clamp(take, 1, 10000))
            .ToListAsync(ct);
}

public sealed class DecisionTraceRepository(DipDbContext db) : IDecisionTraceRepository
{
    public Task AddAsync(DecisionTrace trace, CancellationToken ct) => db.DecisionTraces.AddAsync(trace, ct).AsTask();

    public async Task<IReadOnlyList<DecisionTrace>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) =>
        await db.DecisionTraces.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
}

public sealed class AdaptiveQaPlanRepository(DipDbContext db) : IAdaptiveQaPlanRepository
{
    public Task AddAsync(AdaptiveQaPlanSnapshot plan, CancellationToken ct) => db.AdaptiveQaPlanSnapshots.AddAsync(plan, ct).AsTask();

    public Task<AdaptiveQaPlanSnapshot?> GetLatestAsync(Guid projectId, CancellationToken ct) =>
        db.AdaptiveQaPlanSnapshots.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
}
