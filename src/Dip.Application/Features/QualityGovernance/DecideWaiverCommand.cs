// Purpose: Defines the DecideWaiverCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record DecideWaiverCommand(string Reason,long? ExpectedVersion=null);
