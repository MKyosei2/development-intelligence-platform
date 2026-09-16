// Purpose: Defines EffectivePolicy.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record EffectivePolicy(EffectiveEvidencePolicy Evidence,EffectiveAgentPolicy Agent,EffectiveRetentionPolicy Retention,EffectiveAlertPolicy Alert,EffectivePrivacyPolicy Privacy,Guid? QaPolicyId,IReadOnlyList<Guid> AppliedProfileIds);
