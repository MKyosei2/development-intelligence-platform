// Purpose: Defines the TrackedArtifactDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Artifacts;

public sealed record TrackedArtifactDto(Guid Id,Guid ProjectId,string DisplayPath,string Extension,TrackedArtifactLifecycleState State,int CurrentVersionNumber,string? CurrentSha256,long? CurrentSizeBytes,DateTimeOffset? CurrentFileModifiedAtUtc,DateTimeOffset UpdatedAtUtc);
