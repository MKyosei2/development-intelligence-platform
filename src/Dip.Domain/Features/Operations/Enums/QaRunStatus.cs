// Purpose: Defines QA Run Status domain values and invariants.
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
