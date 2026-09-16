// Purpose: Defines the EvidencePolicySettings application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record EvidencePolicySettings(bool? CollectMetadata=null,bool? CalculateContentHash=null,bool? AllowRawArtifactUpload=null);
