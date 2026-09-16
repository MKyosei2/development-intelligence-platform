// Purpose: Coordinates P ro du ct io nT oo lE ve nt Ma pp in g application behavior for ProductionTools.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProductionTools;

public static class ProductionToolEventMapping
{
    public static EvidenceSourceType SourceType(ProductionToolSource source) => source switch
    {
        ProductionToolSource.Maya => EvidenceSourceType.Maya,
        ProductionToolSource.Unity => EvidenceSourceType.Unity,
        ProductionToolSource.Unreal => EvidenceSourceType.Unreal,
        _ => EvidenceSourceType.Collector
    };

    public static EvidenceEventType EventType(ProductionToolSource source, ProductionToolEventKind kind)
        => (source, kind) switch
        {
            (ProductionToolSource.Maya, ProductionToolEventKind.SceneOpened) => EvidenceEventType.MayaSceneOpened,
            (ProductionToolSource.Maya, ProductionToolEventKind.SceneSaved) => EvidenceEventType.MayaSceneSaved,
            (ProductionToolSource.Maya, ProductionToolEventKind.Imported) => EvidenceEventType.MayaSceneImported,
            (ProductionToolSource.Maya, ProductionToolEventKind.ReferenceChanged) => EvidenceEventType.MayaReferenceChanged,
            (ProductionToolSource.Maya, ProductionToolEventKind.Exported) => EvidenceEventType.MayaExported,
            (ProductionToolSource.Maya, ProductionToolEventKind.RenderCompleted) => EvidenceEventType.RenderCompleted,
            (ProductionToolSource.Maya, ProductionToolEventKind.PlayblastCompleted) => EvidenceEventType.MayaPlayblast,
            (ProductionToolSource.Unity, ProductionToolEventKind.AssetImported) => EvidenceEventType.UnityAssetImported,
            (ProductionToolSource.Unity, ProductionToolEventKind.BuildCompleted) => EvidenceEventType.UnityBuild,
            (ProductionToolSource.Unity, ProductionToolEventKind.TestsCompleted) => EvidenceEventType.UnityTestsCompleted,
            (ProductionToolSource.Unity, ProductionToolEventKind.PerformanceCaptured) => EvidenceEventType.UnityProfilerSample,
            (ProductionToolSource.Unreal, ProductionToolEventKind.AssetImported) => EvidenceEventType.UnrealAssetChanged,
            (ProductionToolSource.Unreal, ProductionToolEventKind.BuildCompleted) => EvidenceEventType.UnrealBuild,
            (ProductionToolSource.Unreal, ProductionToolEventKind.CookCompleted) => EvidenceEventType.UnrealCook,
            (ProductionToolSource.Unreal, ProductionToolEventKind.PackageCompleted) => EvidenceEventType.UnrealPackage,
            (ProductionToolSource.Unreal, ProductionToolEventKind.AutomationTestsCompleted) => EvidenceEventType.UnrealAutomationTests,
            (_, ProductionToolEventKind.ScreenshotGenerated) => EvidenceEventType.ScreenshotGenerated,
            (_, ProductionToolEventKind.VideoGenerated) => EvidenceEventType.VideoGenerated,
            (_, ProductionToolEventKind.RenderCompleted) => EvidenceEventType.RenderCompleted,
            _ => EvidenceEventType.ToolEvent
        };

    public static string EvidenceKind(ProductionToolSource source, ProductionToolEventKind kind)
        => $"production.{source.ToString().ToLowerInvariant()}.{kind.ToString().ToLowerInvariant()}";

    public static string Title(ProductionToolObservation item)
        => $"{item.Source}: {Humanize(item.Kind)}";

    public static string Summary(ProductionToolObservation item)
    {
        var target = !string.IsNullOrWhiteSpace(item.RelativePath)
            ? item.RelativePath
            : !string.IsNullOrWhiteSpace(item.MediaRelativePath)
                ? item.MediaRelativePath
                : "project";
        return $"{item.Source} {Humanize(item.Kind).ToLowerInvariant()} observed for '{target}'.";
    }

    private static string Humanize(ProductionToolEventKind kind)
    {
        var text = kind.ToString();
        if (text.Length == 0) return "event";
        var output = new System.Text.StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]) && char.IsLower(text[i - 1])) output.Append(' ');
            output.Append(text[i]);
        }
        return output.ToString();
    }
}
