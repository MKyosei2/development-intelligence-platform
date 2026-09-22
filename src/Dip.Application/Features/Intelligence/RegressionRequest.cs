// Purpose: Defines the RegressionRequest application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record RegressionRequest(Guid ProjectId, string SubjectKey, string GoodCommit, string BadCommit, string WorkspaceRelativePath, string VerificationCommand, IReadOnlyList<string> VerificationArguments, int TimeoutSeconds = 300);
