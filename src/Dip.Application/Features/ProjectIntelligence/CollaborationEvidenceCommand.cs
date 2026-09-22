// Purpose: Defines the CollaborationEvidenceCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record CollaborationEvidenceCommand(Guid ProjectId, string Provider, string ExternalId, string? Channel, string? Author, string Text, DateTimeOffset ObservedAtUtc, string? Url = null, IReadOnlyList<string>? References = null, string? Revision = null);
