// Purpose: Defines the SourceProvenanceDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record SourceProvenanceDto(string Provider, string SourceId, string Revision, string? Url, DateTimeOffset ObservedAtUtc, string Confidence, string RawSha256, string? Channel = null, string? Author = null);
