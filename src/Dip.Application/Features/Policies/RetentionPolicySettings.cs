// Purpose: Defines the RetentionPolicySettings application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record RetentionPolicySettings(int? MetadataRetentionDays=null,int? ArtifactRetentionDays=null,int? VideoRetentionDays=null);
