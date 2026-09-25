// Purpose: Defines P 2 Intelligence Status domain behavior and invariants for common.
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
