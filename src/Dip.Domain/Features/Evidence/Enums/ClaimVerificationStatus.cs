// Purpose: Defines C la im Ve ri fi ca ti on St at us domain behavior and invariants for e vi de nc e.
namespace Dip.Domain.Enums;

public enum ClaimVerificationStatus
{
    Unverified = 0,
    Confirmed = 1,
    PartiallyConfirmed = 2,
    Contradicted = 3,
    Stale = 4
}
