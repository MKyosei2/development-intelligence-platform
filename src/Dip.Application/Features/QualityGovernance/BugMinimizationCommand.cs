// Purpose: Defines the BugMinimizationCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record BugMinimizationCommand(Guid ProjectId,Guid FindingId,string WorkspaceRelativePath,string Executable,IReadOnlyList<string> Arguments,int TimeoutSeconds=30);
