// Purpose: Defines the ProjectPlanImportResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectPlanImportResult(Guid ProjectId,string ImportKey,string FileName,string SourceSha256,int ImportedItems,int ParentLinks,int DependencyLinks,int AutoResolvedEntities,IReadOnlyList<string> Warnings,DateTimeOffset ImportedAtUtc);
