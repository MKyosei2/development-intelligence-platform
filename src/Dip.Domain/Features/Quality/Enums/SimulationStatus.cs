// Purpose: Defines S im ul at io nS ta tu s domain behavior and invariants for q ua li ty.
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
