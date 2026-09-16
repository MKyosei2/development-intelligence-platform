// Purpose: Defines the PlanSheetAnalysisDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record PlanSheetAnalysisDto(string SheetName,PlanColumnMappingDto Mapping,int DataRows,IReadOnlyList<PlanItemCandidateDto> Preview);
