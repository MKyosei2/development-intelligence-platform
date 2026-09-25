// Purpose: Defines Evidence Outcome domain behavior and invariants for evidence.
namespace Dip.Domain.Enums;

public enum EvidenceOutcome
{
    Unknown = 0,
    Succeeded = 1,
    Warning = 2,
    Failed = 3,
    Changed = 4,
    Informational = 5
}
