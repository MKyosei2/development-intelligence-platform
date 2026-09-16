// Purpose: Defines the ProjectPlanAnalysisDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectPlanAnalysisDto(string FileName,string SourceSha256,IReadOnlyList<PlanSheetAnalysisDto> Sheets,int CandidateItems,IReadOnlyList<string> Warnings,DateTimeOffset AnalyzedAtUtc);
