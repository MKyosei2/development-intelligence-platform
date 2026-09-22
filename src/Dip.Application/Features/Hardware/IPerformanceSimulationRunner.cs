// Purpose: Defines the IPerformanceSimulationRunner application boundary.
namespace Dip.Application.Features.Hardware;

public interface IPerformanceSimulationRunner { Task<PerformanceSimulationMeasurement> RunAsync(HardwareProfileDto profile, string workspace, string executable, IReadOnlyList<string> arguments, int durationSeconds, bool thermalSimulation, string artifactDirectory, CancellationToken cancellationToken); }
