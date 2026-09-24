// Purpose: Persists and queries hardware profiles used by QA and performance analysis.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class HardwareProfileRepository(DipDbContext db) : IHardwareProfileRepository
{
    public Task AddAsync(HardwareProfile profile, CancellationToken ct)
        => db.HardwareProfiles.AddAsync(profile, ct).AsTask();

    public Task<HardwareProfile?> GetAsync(Guid id, CancellationToken ct)
        => db.HardwareProfiles.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<HardwareProfile?> FindAsync(
        string name,
        string platform,
        CancellationToken ct)
        => db.HardwareProfiles.SingleOrDefaultAsync(
            x => x.Name == name && x.Platform == platform,
            ct);

    public async Task<IReadOnlyList<HardwareProfile>> ListAsync(CancellationToken ct)
        => await db.HardwareProfiles
            .AsNoTracking()
            .OrderBy(x => x.Platform)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
}
