// Purpose: Defines the ProjectSourceObservation application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectSourceObservation(string Provider, string ExternalId, string Kind, string Title, string? Body, string Status, decimal? ProgressPercent, string? Owner, DateTimeOffset? StartAtUtc, DateTimeOffset? DueAtUtc, DateTimeOffset UpdatedAtUtc, string Revision, string? Url, IReadOnlyList<string> References, IReadOnlyDictionary<string, string>? Fields = null);
