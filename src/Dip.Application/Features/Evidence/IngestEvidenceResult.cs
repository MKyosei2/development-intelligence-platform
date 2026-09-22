// Purpose: Defines the IngestEvidenceResult application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.Evidence;

public sealed record IngestEvidenceResult(Guid EvidenceId, bool Created, Guid ProjectId, EvidenceSourceType SourceType, string SourceInstanceId, string SourceEventId, EvidenceEventType EventType, DateTimeOffset ObservedAtUtc, DateTimeOffset StoredAtUtc);
