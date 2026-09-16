// Purpose: Defines the DecideReleaseGateOverrideCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record DecideReleaseGateOverrideCommand(string Reason,long? ExpectedVersion=null);
