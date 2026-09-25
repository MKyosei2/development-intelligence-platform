// Purpose: Defines Reproduction Status domain behavior and invariants for production.
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
