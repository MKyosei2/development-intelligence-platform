// Purpose: Defines the IngestEvidenceCommand application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Evidence;

public sealed record IngestEvidenceCommand(Guid ProjectId, EvidenceSourceType SourceType, string SourceInstanceId, string SourceEventId, EvidenceEventType EventType, string SubjectKey, string EvidenceKind, string Title, string Summary, EvidenceOutcome Outcome, EvidenceConfidence Confidence, DateTimeOffset ObservedAtUtc, Guid? PipelineRunId = null, string? SourceReference = null, string? ActorId = null, string? MachineId = null, string? ArtifactId = null, string? ContentHash = null, Guid? ParentEvidenceId = null, EvidencePrivacyClass PrivacyClass = EvidencePrivacyClass.Internal, JsonElement? Metadata = null, JsonElement? Payload = null);
