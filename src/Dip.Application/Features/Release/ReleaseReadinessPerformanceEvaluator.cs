// Purpose: Evaluates current platform performance budget evidence for release readiness.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;

internal sealed class ReleaseReadinessPerformanceEvaluator(IPerformanceBudgetRepository budgets)
{
    public async Task EvaluateAsync(ReleaseReadinessEvaluationContext context, CancellationToken ct)
    {
        var evaluations = await budgets.ListEvaluationsAsync(context.Request.ProjectId, 50, ct);
        context.PerformanceEvaluationCount = evaluations.Count;

        if (evaluations.Any(x => x.Verdict == PerformanceBudgetVerdict.Failed))
        {
            context.Blockers.Add("At least one recent platform performance budget evaluation failed.");
            context.Actions.Add("Restore the failed platform performance budget and rerun the affected benchmark/replay.");
        }
        else if (evaluations.Any(x => x.Verdict == PerformanceBudgetVerdict.Warning))
        {
            context.Warnings.Add("A recent performance budget is near its configured boundary.");
        }
    }
}
