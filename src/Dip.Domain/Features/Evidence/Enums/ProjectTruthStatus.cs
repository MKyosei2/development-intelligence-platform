// Purpose: Defines P ro je ct Tr ut hS ta tu s states used by Evidence.
namespace Dip.Domain.Enums;

public enum ProjectTruthStatus
{
    Unknown = 0,
    Healthy = 1,
    Attention = 2,
    AtRisk = 3,
    Blocked = 4
}
