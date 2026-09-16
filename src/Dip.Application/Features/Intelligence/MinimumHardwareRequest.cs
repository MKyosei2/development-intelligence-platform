// Purpose: Defines the MinimumHardwareRequest application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record MinimumHardwareRequest(Guid ProjectId,string SubjectKey,string WorkspaceRelativePath,string RuntimeExecutable,IReadOnlyList<string> RuntimeArguments,int TargetFps,long MaximumMemoryMb,int MaxIterations=8);
