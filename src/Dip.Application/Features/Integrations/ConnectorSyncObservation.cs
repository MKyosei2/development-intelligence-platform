// Purpose: Defines the ConnectorSyncObservation application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ConnectorSyncObservation(EvidenceSourceType SourceType,string SourceInstanceId,string SourceEventId,EvidenceEventType EventType,string SubjectKey,string EvidenceKind,string Title,string Summary,EvidenceOutcome Outcome,EvidenceConfidence Confidence,DateTimeOffset ObservedAtUtc,string? SourceReference=null,string? ActorId=null,string? MetadataJson=null);
