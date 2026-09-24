// Purpose: Defines R ep ro du ct io nS ta tu s domain behavior and invariants for p ro du ct io n.
namespace Dip.Domain.Enums;

public enum ReproductionStatus
{
    NotRequested,
    Queued,
    Preparing,
    Recording,
    Running,
    CollectingEvidence,
    Analyzing,
    Reproduced,
    NotReproduced,
    TimedOut,
    EnvironmentFailure,
    InfrastructureFailure
}
