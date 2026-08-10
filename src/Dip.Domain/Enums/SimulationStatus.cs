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
