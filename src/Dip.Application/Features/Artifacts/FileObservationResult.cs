// Purpose: Defines the FileObservationResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Artifacts;

public sealed record FileObservationResult(string EventId,Guid ArtifactId,Guid VersionId,int VersionNumber,bool Created,bool EvidenceCreated,string DisplayPath);
