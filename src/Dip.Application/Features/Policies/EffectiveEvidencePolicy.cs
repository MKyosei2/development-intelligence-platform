// Purpose: Defines EffectiveEvidencePolicy.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record EffectiveEvidencePolicy(bool CollectMetadata,bool CalculateContentHash,bool AllowRawArtifactUpload);
