// Purpose: Defines the PolicyLifecycleCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record PolicyLifecycleCommand(string Reason,long? ExpectedVersion=null);
