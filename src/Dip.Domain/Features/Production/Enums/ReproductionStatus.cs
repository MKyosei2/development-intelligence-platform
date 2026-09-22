// Purpose: Defines domain behavior and state for this feature area.
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
