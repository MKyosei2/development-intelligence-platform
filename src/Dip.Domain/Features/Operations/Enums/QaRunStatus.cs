// Purpose: Defines QA Run Status domain behavior and invariants for operations.
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
