// Purpose: Defines the RootCauseRequest application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record RootCauseRequest(Guid ProjectId, string SubjectKey, string FindingText, string StackTrace, string DiffText, IReadOnlyList<string> LogLines, bool IncludeSuggestedAction = true);
