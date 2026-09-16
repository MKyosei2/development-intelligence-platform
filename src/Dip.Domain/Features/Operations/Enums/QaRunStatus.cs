// Purpose: Defines Q aR un St at us states used by Operations.
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
