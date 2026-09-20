// Purpose: Defines the Project Truth health states emitted by evidence-backed project evaluation.
namespace Dip.Domain.Enums;

public enum ProjectTruthStatus
{
    Unknown = 0,
    Healthy = 1,
    Attention = 2,
    AtRisk = 3,
    Blocked = 4
}
