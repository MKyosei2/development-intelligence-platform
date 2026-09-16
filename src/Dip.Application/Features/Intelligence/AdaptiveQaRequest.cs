// Purpose: Defines the AdaptiveQaRequest application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record AdaptiveQaRequest(Guid ProjectId,string SubjectKey,IReadOnlyList<string> ChangedPaths,int BudgetMinutes);
