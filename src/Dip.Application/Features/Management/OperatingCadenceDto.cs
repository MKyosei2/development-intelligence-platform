// Purpose: Defines the OperatingCadenceDto application contract.
namespace Dip.Application.Features.Management;

public sealed record OperatingCadenceDto(string Mode,string Title,string Summary,IReadOnlyList<string> TalkingPoints,IReadOnlyList<CockpitDecisionDto> Decisions,IReadOnlyList<string> FollowUps,DateTimeOffset GeneratedAtUtc);
