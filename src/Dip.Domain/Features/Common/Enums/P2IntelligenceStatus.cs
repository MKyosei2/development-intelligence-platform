// Purpose: Defines P2 In te ll ig en ce St at us domain behavior and invariants for this repository area.
namespace Dip.Domain.Enums;

public enum P2IntelligenceStatus
{
    Unknown = 0,
    Passed = 1,
    Warning = 2,
    Failed = 3,
    Informational = 4,
    InfrastructureFailure = 5
}
