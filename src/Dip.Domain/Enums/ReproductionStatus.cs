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
