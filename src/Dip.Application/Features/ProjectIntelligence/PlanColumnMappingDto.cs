// Purpose: Defines the PlanColumnMappingDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record PlanColumnMappingDto(string SheetName, int HeaderRowIndex, int? WbsColumn, int? TitleColumn, int? ParentColumn, int? StartColumn, int? DueColumn, int? ProgressColumn, int? OwnerColumn, int? StatusColumn, int? DependencyColumn, int? MilestoneColumn, decimal Confidence, IReadOnlyList<string> Headers, IReadOnlyList<string> Notes);
