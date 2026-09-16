// Purpose: Defines the PlanItemCandidateDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record PlanItemCandidateDto(string Key,string Name,string? ParentKey,DateTimeOffset? PlannedStartUtc,DateTimeOffset? DueAtUtc,decimal? DeclaredProgressPercent,string? Owner,string? Status,IReadOnlyList<string> DependencyKeys,bool IsMilestone,string SheetName,int SourceRow,string SourceReference);
