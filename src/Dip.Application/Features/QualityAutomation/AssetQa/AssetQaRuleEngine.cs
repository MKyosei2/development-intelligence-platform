using System.Text.Json;
using Dip.Application.Features.ProductionTools;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation.AssetQa;

public sealed class AssetQaRuleEngine
{
    public IReadOnlyList<AssetQaRuleResult> Evaluate(ProductionToolObservation observation, string rulesJson)
    {
        if (observation.Metadata is not { } metadata || metadata.ValueKind != JsonValueKind.Object)
            return Array.Empty<AssetQaRuleResult>();
        using var config = ParseConfig(rulesJson);
        var list = new List<AssetQaRuleResult>();
        switch (observation.Source)
        {
            case ProductionToolSource.Maya:
                EvaluateMaya(observation, metadata, config.RootElement, list);
                break;
            case ProductionToolSource.Unity:
                EvaluateUnity(observation, metadata, config.RootElement, list);
                break;
            case ProductionToolSource.Unreal:
                EvaluateUnreal(observation, metadata, config.RootElement, list);
                break;
        }
        return list;
    }

    private static void EvaluateMaya(ProductionToolObservation o, JsonElement m, JsonElement c, List<AssetQaRuleResult> output)
    {
        var cfg = Child(c, "maya");
        var missing = ArrayCount(m, "missingTextures");
        if (missing > 0)
            output.Add(Result("maya.missing-texture", Severity(cfg, "missingTextureSeverity", AssetQaSeverity.Critical),
                "Missing Maya texture reference", $"{missing} texture reference(s) are missing.", o.RelativePath, new Dictionary<string, object?> { ["missingTextureCount"] = missing }));

        var unknown = Int(m, "unknownNodeCount");
        if (unknown > 0)
            output.Add(Result("maya.unknown-node", Severity(cfg, "unknownNodeSeverity", AssetQaSeverity.Warning),
                "Unknown Maya nodes", $"{unknown} unknown node(s) are present in the scene.", o.RelativePath, new Dictionary<string, object?> { ["unknownNodeCount"] = unknown }));

        var polygons = Int(m, "polygonCount");
        var budget = Int(cfg, "polygonBudget", 500000);
        if (polygons > budget)
            output.Add(Result("maya.polygon-budget", AssetQaSeverity.Warning,
                "Maya polygon budget exceeded", $"Scene contains {polygons:N0} polygons; project rule allows {budget:N0}.", o.RelativePath,
                new Dictionary<string, object?> { ["polygonCount"] = polygons, ["polygonBudget"] = budget }));
    }

    private static void EvaluateUnity(ProductionToolObservation o, JsonElement m, JsonElement c, List<AssetQaRuleResult> output)
    {
        var cfg = Child(c, "unity");
        var missingScripts = Int(m, "missingScriptCount");
        if (missingScripts > 0)
            output.Add(Result("unity.missing-script", Severity(cfg, "missingScriptSeverity", AssetQaSeverity.Critical),
                "Missing Unity script reference", $"Asset contains {missingScripts} missing script reference(s).", o.RelativePath,
                new Dictionary<string, object?> { ["missingScriptCount"] = missingScripts }));

        var width = Int(m, "textureWidth");
        var height = Int(m, "textureHeight");
        var max = Int(cfg, "maxTextureDimension", 4096);
        if (Math.Max(width, height) > max)
            output.Add(Result("unity.texture-resolution", AssetQaSeverity.Warning,
                "Unity texture exceeds resolution limit", $"Texture is {width}x{height}; project rule allows a maximum dimension of {max}.", o.RelativePath,
                new Dictionary<string, object?> { ["width"] = width, ["height"] = height, ["maxDimension"] = max }));

        var requireLod = Bool(cfg, "requireLodForMeshes");
        var assetType = String(m, "assetType") ?? string.Empty;
        var lodCount = Int(m, "lodGroupCount");
        if (requireLod && lodCount == 0 && (assetType.Contains("GameObject", StringComparison.OrdinalIgnoreCase) || assetType.Contains("Mesh", StringComparison.OrdinalIgnoreCase)))
            output.Add(Result("unity.lod-required", AssetQaSeverity.Warning,
                "Unity LOD is required", "Project policy requires an LODGroup for mesh/prefab assets.", o.RelativePath,
                new Dictionary<string, object?> { ["assetType"] = assetType, ["lodGroupCount"] = lodCount }));
    }

    private static void EvaluateUnreal(ProductionToolObservation o, JsonElement m, JsonElement c, List<AssetQaRuleResult> output)
    {
        var cfg = Child(c, "unreal");
        var missing = Int(m, "missingDependencyCount");
        if (missing > 0)
            output.Add(Result("unreal.missing-dependency", Severity(cfg, "missingDependencySeverity", AssetQaSeverity.Critical),
                "Missing Unreal dependency", $"Asset reports {missing} missing dependency reference(s).", o.RelativePath,
                new Dictionary<string, object?> { ["missingDependencyCount"] = missing }));
        if (Bool(m, "isRedirector"))
            output.Add(Result("unreal.redirector", Severity(cfg, "redirectorSeverity", AssetQaSeverity.Warning),
                "Unreal redirector requires cleanup", "Asset is a redirector; fix redirectors before release packaging.", o.RelativePath,
                new Dictionary<string, object?> { ["assetClass"] = String(m, "assetClass") }));
    }

    private static AssetQaRuleResult Result(string id, AssetQaSeverity severity, string title, string message, string? path, IReadOnlyDictionary<string, object?> details)
        => new(id, severity, title, message, path, details);

    private static JsonDocument ParseConfig(string json)
    {
        try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); }
        catch (JsonException) { return JsonDocument.Parse("{}"); }
    }

    private static JsonElement Child(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;

    private static int Int(JsonElement root, string name, int fallback = 0)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    private static bool Bool(JsonElement root, string name)
        {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value)) return false;
        return (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) && value.GetBoolean();
    }

    private static string? String(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int ArrayCount(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;

    private static AssetQaSeverity Severity(JsonElement root, string name, AssetQaSeverity fallback)
        => Enum.TryParse<AssetQaSeverity>(String(root, name), true, out var parsed) ? parsed : fallback;
}
