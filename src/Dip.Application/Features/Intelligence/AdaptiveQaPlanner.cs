using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Intelligence;

public sealed class AdaptiveQaPlanner
{
    private readonly IEvidenceRepository _evidence;

    public AdaptiveQaPlanner(IEvidenceRepository evidence) => _evidence = evidence;

    public async Task<AdaptiveQaPlan> PlanAsync(AdaptiveQaRequest request, CancellationToken cancellationToken)
    {
        var evidence = await _evidence.ListForProjectAsync(
            request.ProjectId,
            DateTimeOffset.UtcNow.AddDays(-90),
            cancellationToken);
        var failures = evidence.Count(x => x.Outcome == EvidenceOutcome.Failed);
        var total = Math.Max(1, evidence.Count);
        var historicalRisk = Math.Min(100m, decimal.Round(100m * failures / total, 2));
        var changedPathText = string.Join('|', request.ChangedPaths).ToLowerInvariant();
        var items = new List<QaPlanItem>();

        void Add(string kind, string reason, int minutes, decimal risk, bool required = false) =>
            items.Add(new(kind, reason, minutes, risk, required));

        if (request.ChangedPaths.Count == 0 ||
            changedPathText.Contains("readme") && request.ChangedPaths.All(x => x.Contains("readme", StringComparison.OrdinalIgnoreCase)))
        {
            Add("fast-sanity", "Documentation-only change; full QA is not justified.", 1, 5, true);
        }
        else
        {
            Add("language-build-test", "Source changed; compile and focused tests are baseline evidence.", 4, 55, true);
            if (changedPathText.Contains("render") || changedPathText.Contains("shader") || changedPathText.Contains("material") || changedPathText.Contains("ui"))
            {
                Add("visual-regression", "Rendering/UI paths changed.", 5, 80, true);
                Add("gpu-performance", "Rendering change can affect frame time.", 8, 75);
            }
            if (changedPathText.Contains("android") || changedPathText.Contains("mobile") || changedPathText.Contains("ios"))
            {
                Add("mobile-envelope", "Mobile-targeted files changed.", 12, 90, true);
                Add("thermal-soak", "Mobile changes carry thermal risk.", 30, 75);
            }
            if (changedPathText.Contains("memory") || changedPathText.Contains("alloc") || changedPathText.Contains("stream"))
                Add("memory-soak", "Memory-sensitive path changed.", 20, 85, true);
            if (historicalRisk > 20)
                Add("historical-risk-repeat", "Recent project evidence has elevated failure history.", 10, Math.Min(100, historicalRisk + 30), true);
        }

        var selected = items.Where(x => x.Required).ToList();
        foreach (var item in items.Where(x => !x.Required).OrderByDescending(x => x.RiskWeight))
        {
            if (selected.Sum(x => x.EstimatedMinutes) + item.EstimatedMinutes <= Math.Max(1, request.BudgetMinutes))
                selected.Add(item);
        }

        return new(
            selected,
            selected.Sum(x => x.EstimatedMinutes),
            historicalRisk,
            $"Planned from {request.ChangedPaths.Count} changed path(s), {evidence.Count} recent evidence item(s), and a {historicalRisk:0.##}% historical failure ratio.");
    }
}
