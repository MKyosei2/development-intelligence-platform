// Purpose: Defines the IHardwareCalibrationRunner application boundary.
namespace Dip.Application.Features.Hardware;

public interface IHardwareCalibrationRunner { Task<HardwareCalibrationMeasurement> CalibrateAsync(string artifactDirectory, CancellationToken cancellationToken); }
