// Purpose: Defines the allowed S im ul at io nS ta tu s states used by the DIP domain.
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
