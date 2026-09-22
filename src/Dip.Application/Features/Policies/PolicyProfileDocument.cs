// Purpose: Defines the PolicyProfileDocument application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record PolicyProfileDocument(string Name, bool Enabled, EvidencePolicySettings Evidence, AgentPolicySettings Agent, RetentionPolicySettings Retention, AlertPolicySettings Alert, PrivacyPolicySettings Privacy, Guid? QaPolicyId = null);
