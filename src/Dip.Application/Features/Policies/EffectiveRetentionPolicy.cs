// Purpose: Defines EffectiveRetentionPolicy.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record EffectiveRetentionPolicy(int MetadataRetentionDays,int ArtifactRetentionDays,int VideoRetentionDays);
