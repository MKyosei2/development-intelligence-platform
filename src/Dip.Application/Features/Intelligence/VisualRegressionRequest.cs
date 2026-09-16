// Purpose: Defines the VisualRegressionRequest application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record VisualRegressionRequest(Guid ProjectId,string SubjectKey,string WorkspaceRelativePath,string BaselinePath,string CurrentPath,decimal ThresholdPercent=1m);
