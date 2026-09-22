// Purpose: Defines the RegressionResult application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record RegressionResult(string? FirstBadCommit, IReadOnlyList<string> TestedCommits, string Status, string EvidenceJson);
