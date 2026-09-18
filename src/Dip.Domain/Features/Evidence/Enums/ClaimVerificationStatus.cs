// Purpose: Defines the allowed C la im Ve ri fi ca ti on St at us states used by the DIP domain.
namespace Dip.Domain.Enums;

public enum ClaimVerificationStatus
{
    Unverified = 0,
    Confirmed = 1,
    PartiallyConfirmed = 2,
    Contradicted = 3,
    Stale = 4
}
