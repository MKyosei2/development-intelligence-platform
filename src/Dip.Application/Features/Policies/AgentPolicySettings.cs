// Purpose: Defines the AgentPolicySettings application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record AgentPolicySettings(bool? MayaMonitoringEnabled = null, bool? UnityMonitoringEnabled = null, bool? UnrealMonitoringEnabled = null);
