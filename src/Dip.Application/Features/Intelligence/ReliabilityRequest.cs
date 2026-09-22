// Purpose: Defines the ReliabilityRequest application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record ReliabilityRequest(Guid ProjectId, string SubjectKey, string WorkspaceRelativePath, string Executable, IReadOnlyList<string> Arguments, int RepeatCount = 20, int SoakMinutes = 0);
