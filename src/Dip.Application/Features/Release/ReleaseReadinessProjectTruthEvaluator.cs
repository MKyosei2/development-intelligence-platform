// Purpose: Evaluates Project Truth blockers and unverified claims for release readiness.
using Dip.Application.Abstractions.Persistence;

namespace Dip.Application.Features.Release;

internal sealed class ReleaseReadinessProjectTruthEvaluator(IProjectTruthRepository truth)
{
    public async Task EvaluateAsync(ReleaseReadinessEvaluationContext context, CancellationToken ct)
    {
        var snapshot = await truth.GetLatestAsync(context.Request.ProjectId, ct);
        context.HasProjectTruth = snapshot is not null;
        if (snapshot is null)
        {
            context.Warnings.Add("Project Truth has not been evaluated yet.");
            return;
        }

        if (snapshot.BlockerCount > 0)
        {
            context.Blockers.Add($"Project Truth reports {snapshot.BlockerCount} blocker(s).");
            context.Actions.Add("Resolve Project Truth blockers before release.");
        }
        if (snapshot.UnverifiedClaimCount > 0)
            context.Warnings.Add($"{snapshot.UnverifiedClaimCount} project claim(s) remain unverified.");
    }
}
