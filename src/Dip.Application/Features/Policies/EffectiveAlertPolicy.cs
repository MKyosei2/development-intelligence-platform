// Purpose: Defines EffectiveAlertPolicy.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record EffectiveAlertPolicy(int CriticalThreshold, bool NotifyOnContradiction);
