// Purpose: Defines the QaFindingStateDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaFindingStateDto(Guid Id,Guid ProjectId,string Fingerprint,Guid LastFindingId,string SubjectKey,string? LastCommitSha,FindingLifecycleStatus Status,int Occurrences,string? Team,string? Workstream,QaOwnershipSource OwnershipSource,string? DispositionReason,string? UpdatedBy,DateTimeOffset FirstSeenAtUtc,DateTimeOffset LastSeenAtUtc,DateTimeOffset UpdatedAtUtc,long Version);
