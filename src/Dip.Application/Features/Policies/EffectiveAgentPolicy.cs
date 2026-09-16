// Purpose: Defines EffectiveAgentPolicy.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record EffectiveAgentPolicy(bool MayaMonitoringEnabled,bool UnityMonitoringEnabled,bool UnrealMonitoringEnabled);
