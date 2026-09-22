// Purpose: Defines the AlertPolicySettings application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record AlertPolicySettings(int? CriticalThreshold = null, bool? NotifyOnContradiction = null);
