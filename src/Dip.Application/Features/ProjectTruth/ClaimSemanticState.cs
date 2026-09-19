// Purpose: Defines the canonical semantic states used to interpret human project claims.
namespace Dip.Application.Features.ProjectTruth;

public enum ClaimSemanticState
{
    Unknown,
    NotStarted,
    InProgress,
    Ready,
    Complete,
    Blocked,
    Failed
}
