// Purpose: Defines S im ul at io nS ta tu s states used by Quality.
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
