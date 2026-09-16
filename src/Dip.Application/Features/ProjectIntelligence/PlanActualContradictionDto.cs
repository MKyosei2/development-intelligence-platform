// Purpose: Defines the PlanActualContradictionDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record PlanActualContradictionDto(string Key,string Severity,string Title,string Explanation,string RecommendedAction,IReadOnlyList<string> Sources);
