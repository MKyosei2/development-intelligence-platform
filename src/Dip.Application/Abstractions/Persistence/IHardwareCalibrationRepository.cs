// Purpose: Defines the IHardwareCalibrationRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IHardwareCalibrationRepository
{
    Task AddAsync(HardwareCalibration calibration,CancellationToken ct);
    Task<HardwareCalibration?> GetLatestAsync(Guid projectId,CancellationToken ct);
}
