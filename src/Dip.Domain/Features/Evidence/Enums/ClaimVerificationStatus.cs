// Purpose: Defines Claim Verification Status domain behavior and invariants for evidence.
namespace Dip.Domain.Enums;

public enum ClaimVerificationStatus
{
    Unverified = 0,
    Confirmed = 1,
    PartiallyConfirmed = 2,
    Contradicted = 3,
    Stale = 4
}
