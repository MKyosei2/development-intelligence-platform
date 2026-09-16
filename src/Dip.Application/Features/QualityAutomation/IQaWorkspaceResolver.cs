// Purpose: Defines the IQaWorkspaceResolver application boundary.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public interface IQaWorkspaceResolver{string Resolve(string workspaceRelativePath);string QaArtifactRoot(Guid projectId,Guid qaRunId);}
