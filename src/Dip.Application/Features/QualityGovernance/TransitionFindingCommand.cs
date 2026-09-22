// Purpose: Defines the TransitionFindingCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record TransitionFindingCommand(Guid ProjectId, string Fingerprint, FindingLifecycleStatus Status, string Reason, long? ExpectedVersion = null);
