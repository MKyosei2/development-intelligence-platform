// Purpose: Defines the ProgressEvidenceDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProgressEvidenceDto(string Provider,string Kind,string Summary,string Confidence,string? Url,DateTimeOffset ObservedAtUtc,bool MachineObserved,IReadOnlyList<string>? References=null);
