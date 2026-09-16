// Purpose: Selects focused QA packs from production-tool evidence and change metadata.
using Dip.Domain.Enums;

namespace Dip.Application.Features.Enterprise;

public sealed record AdaptiveQaSelection(
    string PackId,
    string Reason,
    int Priority,
    decimal Confidence);

public sealed class AdaptiveQaRoutingService
{
    public IReadOnlyList<AdaptiveQaSelection> Select(
        EvidenceSourceType source,
        EvidenceEventType eventType,
        string? subjectKey,
        string? metadataJson)
    {
        var rows = new List<AdaptiveQaSelection>();
        var text = ((subjectKey ?? string.Empty) + " " + (metadataJson ?? string.Empty)).ToLowerInvariant();

        void Add(string id, string reason, int priority, decimal confidence)
        {
            if (rows.All(x => !x.PackId.Equals(id, StringComparison.OrdinalIgnoreCase)))
                rows.Add(new(id, reason, priority, confidence));
        }

        if (source is EvidenceSourceType.Maya)
            Add("asset-maya", "Maya scene evidence changed; run structural asset validation.", 90, 95m);

        if (source is EvidenceSourceType.Unity)
        {
            Add("asset-unity", "Unity asset/build evidence changed.", 85, 92m);
            if (text.Contains("shader") || text.Contains("material") || text.Contains("ui"))
                Add("visual-regression", "Rendering-sensitive Unity content changed.", 95, 90m);
        }

        if (source is EvidenceSourceType.Unreal)
        {
            Add("asset-unreal", "Unreal asset/build evidence changed.", 85, 92m);
            if (text.Contains("material") || text.Contains("widget") || text.Contains("lighting"))
                Add("visual-regression", "Rendering-sensitive Unreal content changed.", 95, 90m);
        }

        if (eventType is EvidenceEventType.ScreenshotGenerated or EvidenceEventType.RenderCompleted)
            Add("visual-regression", "New visual evidence is available for Golden Master comparison.", 100, 98m);
        if (eventType == EvidenceEventType.UnityBuild || text.Contains("build"))
            Add("build-smoke", "Build-related evidence changed; validate build/runtime smoke path.", 80, 90m);
        if (text.Contains("skeleton") || text.Contains("joint") || text.Contains("rig"))
            Add("animation-retarget", "Skeleton/rig evidence changed.", 88, 90m);
        if (rows.Count == 0)
            Add("focused-smoke", "No specialized impact rule matched; use focused smoke QA.", 40, 60m);

        return rows.OrderByDescending(x => x.Priority).ToArray();
    }
}
