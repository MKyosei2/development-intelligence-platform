// Purpose: Defines domain behavior and state for this feature area.
namespace Dip.Domain.Enums;

public enum SimulationStatus
{
    Queued,
    Running,
    Passed,
    Warning,
    Failed,
    Unsupported,
    InfrastructureFailure,
    Cancelled
}
