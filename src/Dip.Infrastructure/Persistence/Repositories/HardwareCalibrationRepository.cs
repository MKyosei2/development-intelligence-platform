// Purpose: Persists hardware calibration measurements and returns the latest project calibration.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class HardwareCalibrationRepository(DipDbContext db) : IHardwareCalibrationRepository
{
    public Task AddAsync(HardwareCalibration calibration, CancellationToken ct)
        => db.HardwareCalibrations.AddAsync(calibration, ct).AsTask();

    public Task<HardwareCalibration?> GetLatestAsync(Guid projectId, CancellationToken ct)
        => db.HardwareCalibrations
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.MeasuredAtUtc)
            .FirstOrDefaultAsync(ct);
}
