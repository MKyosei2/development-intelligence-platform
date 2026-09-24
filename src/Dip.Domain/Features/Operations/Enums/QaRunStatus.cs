// Purpose: Defines Q aR un St at us domain behavior and invariants for o pe ra ti on s.
namespace Dip.Domain.Enums;

public enum QaRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Partial,
    InfrastructureFailure,
    Cancelled
}
