// Purpose: Defines the allowed Q aR un St at us states used by the DIP domain.
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
