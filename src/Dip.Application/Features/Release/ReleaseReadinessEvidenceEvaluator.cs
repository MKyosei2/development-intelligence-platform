// Purpose: Evaluates fresh project evidence and exact-commit release provenance for release readiness.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;

namespace Dip.Application.Features.Release;

internal sealed class ReleaseReadinessEvidenceEvaluator(
    IEvidenceRepository evidence,
    IReleaseEvidenceRepository releaseEvidence,
    ISystemClock clock)
{
    public async Task EvaluateAsync(ReleaseReadinessEvaluationContext context, CancellationToken ct)
    {
        var request = context.Request;
        var freshnessDays = Math.Clamp(request.EvidenceFreshnessDays, 1, 90);
        var fresh = await evidence.ListForProjectAsync(
            request.ProjectId,
            clock.UtcNow.AddDays(-freshnessDays),
            ct);
        context.FreshEvidenceCount = fresh.Count;

        if (fresh.Count == 0)
        {
            context.Blockers.Add("No fresh release evidence exists in the configured freshness window.");
            context.Actions.Add("Run the release QA/benchmark/replay suite to generate fresh evidence.");
        }

        var bundle = await releaseEvidence.GetLatestAsync(request.ProjectId, ct);
        if (bundle is null || !string.Equals(bundle.CommitSha, request.CommitSha, StringComparison.OrdinalIgnoreCase))
        {
            context.Warnings.Add("No release provenance/SBOM bundle is attached to this exact commit.");
            context.Actions.Add("Generate a release evidence bundle for the candidate commit.");
        }
    }
}
