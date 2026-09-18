// Purpose: Defines the allowed R ep ro du ct io nS ta tu s states used by the DIP domain.
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
