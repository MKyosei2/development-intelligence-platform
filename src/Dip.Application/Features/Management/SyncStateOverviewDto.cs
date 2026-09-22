// Purpose: Defines the SyncStateOverviewDto application contract.
namespace Dip.Application.Features.Management;

public sealed record SyncStateOverviewDto(Guid ProjectId, IReadOnlyList<SourceAuthorityRuleDto> AuthorityRules, IReadOnlyList<SyncConflictDto> Conflicts, int OpenConflicts, DateTimeOffset GeneratedAtUtc);
