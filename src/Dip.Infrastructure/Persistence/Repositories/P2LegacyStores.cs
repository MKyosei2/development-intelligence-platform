// Purpose: Implements P2 Le ga cy St or es infrastructure integration for p er si st en ce.
using Dip.Application.Features.Intelligence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class CommunityHardwareStore(DipDbContext db) : ICommunityHardwareStore
{
    public Task AddAsync(CommunityHardwareSample sample, CancellationToken ct) => db.Set<CommunityHardwareSample>().AddAsync(sample, ct).AsTask();
    public async Task<IReadOnlyList<CommunityHardwareSample>> ListAsync(string hardwareKey, CancellationToken ct) => await db.Set<CommunityHardwareSample>().AsNoTracking().Where(x => x.HardwareKey == hardwareKey).OrderByDescending(x => x.MeasuredAtUtc).Take(10000).ToListAsync(ct);
}
public sealed class IntelligenceRunStore(DipDbContext db) : IIntelligenceRunStore
{
    public Task AddAsync(IntelligenceRun run, CancellationToken ct) => db.Set<IntelligenceRun>().AddAsync(run, ct).AsTask();
    public Task<IntelligenceRun?> GetAsync(Guid id, CancellationToken ct) => db.Set<IntelligenceRun>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<IntelligenceRun>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) => await db.Set<IntelligenceRun>().AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
}
public sealed class ProjectForecastStore(DipDbContext db) : IProjectForecastStore
{
    public Task AddAsync(ProjectForecast forecast, CancellationToken ct) => db.Set<ProjectForecast>().AddAsync(forecast, ct).AsTask();
    public async Task<IReadOnlyList<ProjectForecast>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) => await db.Set<ProjectForecast>().AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
}
