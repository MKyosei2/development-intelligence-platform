// Purpose: Defines the MinimumHardwareEstimateCommand application contract.
namespace Dip.Application.Features.Hardware;

public sealed record MinimumHardwareEstimateCommand(Guid ProjectId, string SubjectKey, string WorkspaceRelativePath, string Executable, IReadOnlyList<string>? Arguments, int DurationSeconds = 30, int TargetFps = 30, long MemoryBudgetMb = 4096);
