// Purpose: Evaluates Maya, Unity, and Unreal asset metadata with bounded production-oriented QA rules.
using System.Text.Json;
using Dip.Application.Features.ProductionTools;
using Dip.Application.Features.QualityAutomation.VisualQa;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation.AssetQa;

public sealed class AssetQaRuleEngine
{
    // Keep the public entry point routed through AssetQaService so the autonomous
    // dispatcher and durable service share one policy boundary instead of drifting.
    public IReadOnlyList<AssetQaRuleResult> Evaluate(ProductionToolObservation observation, string rulesJson)
        => AssetQaService.EvaluateAutonomous(observation, rulesJson, this).Findings;

    internal IReadOnlyList<AssetQaRuleResult> EvaluateCore(ProductionToolObservation observation, string rulesJson)
    {
        if (observation.Metadata is not { } metadata || metadata.ValueKind != JsonValueKind.Object)
            return Array.Empty<AssetQaRuleResult>();
        var effectiveRules = string.IsNullOrWhiteSpace(rulesJson) || rulesJson.Trim() == "{}"
            ? VisualQaService.DefaultAssetRulesJson
            : rulesJson;
        using var config = ParseConfig(effectiveRules);
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
                "Missing Maya texture reference", $"{missing} texture reference(s) are missing.", o.RelativePath,
                Details(("missingTextureCount", missing))));

        var unknown = Int(m, "unknownNodeCount");
        if (unknown > 0)
            output.Add(Result("maya.unknown-node", Severity(cfg, "unknownNodeSeverity", AssetQaSeverity.Warning),
                "Unknown Maya nodes", $"{unknown} unknown node(s) are present in the scene.", o.RelativePath,
                Details(("unknownNodeCount", unknown))));

        var polygons = Int(m, "polygonCount");
        var budget = Int(cfg, "polygonBudget", 500000);
        if (polygons > budget)
            output.Add(Result("maya.polygon-budget", AssetQaSeverity.Warning,
                "Maya polygon budget exceeded", $"Scene contains {polygons:N0} polygons; project rule allows {budget:N0}.", o.RelativePath,
                Details(("polygonCount", polygons), ("polygonBudget", budget))));

        var nonManifold = Int(m, "nonManifoldCount") + Int(m, "laminaFaceCount");
        if (nonManifold > 0)
            output.Add(Result("maya.non-manifold", Severity(cfg, "nonManifoldSeverity", AssetQaSeverity.Critical),
                "Non-manifold Maya geometry", $"Scene reports {nonManifold} non-manifold/lamina geometry issue(s).", o.RelativePath,
                Details(("nonManifoldOrLaminaCount", nonManifold))));

        var ngons = Int(m, "ngonCount");
        var ngonLimit = Int(cfg, "ngonWarningCount", 100);
        if (ngons > ngonLimit)
            output.Add(Result("maya.ngon-budget", AssetQaSeverity.Warning,
                "Maya n-gon count exceeds project rule", $"Scene reports {ngons} n-gon(s); warning threshold is {ngonLimit}.", o.RelativePath,
                Details(("ngonCount", ngons), ("warningThreshold", ngonLimit))));

        var history = Int(m, "constructionHistoryNodeCount");
        var historyLimit = Int(cfg, "constructionHistoryWarningCount", 200);
        if (history > historyLimit)
            output.Add(Result("maya.history-budget", AssetQaSeverity.Warning,
                "Maya construction history is unusually large", $"Scene reports {history} construction-history node(s); project threshold is {historyLimit}.", o.RelativePath,
                Details(("constructionHistoryNodeCount", history), ("warningThreshold", historyLimit))));

        var unfrozen = Int(m, "unfrozenTransformCount");
        if (unfrozen > 0)
            output.Add(Result("maya.unfrozen-transform", Severity(cfg, "unfrozenTransformSeverity", AssetQaSeverity.Warning),
                "Unfrozen Maya transforms", $"Scene reports {unfrozen} transform(s) with non-identity freeze state.", o.RelativePath,
                Details(("unfrozenTransformCount", unfrozen))));

        var duplicateNames = Int(m, "duplicateShortNameCount");
        if (duplicateNames > 0)
            output.Add(Result("maya.duplicate-name", AssetQaSeverity.Warning,
                "Duplicate Maya short names", $"Scene reports {duplicateNames} duplicate short-name collision(s).", o.RelativePath,
                Details(("duplicateShortNameCount", duplicateNames))));
    }

    private static void EvaluateUnity(ProductionToolObservation o, JsonElement m, JsonElement c, List<AssetQaRuleResult> output)
    {
        var cfg = Child(c, "unity");
        var missingScripts = Int(m, "missingScriptCount");
        if (missingScripts > 0)
            output.Add(Result("unity.missing-script", Severity(cfg, "missingScriptSeverity", AssetQaSeverity.Critical),
                "Missing Unity script reference", $"Asset contains {missingScripts} missing script reference(s).", o.RelativePath,
                Details(("missingScriptCount", missingScripts))));

        var missingMaterials = Int(m, "missingMaterialCount");
        if (missingMaterials > 0)
            output.Add(Result("unity.missing-material", Severity(cfg, "missingMaterialSeverity", AssetQaSeverity.Critical),
                "Missing Unity material reference", $"Asset contains {missingMaterials} missing material reference(s).", o.RelativePath,
                Details(("missingMaterialCount", missingMaterials))));

        var width = Int(m, "textureWidth");
        var height = Int(m, "textureHeight");
        var max = Int(cfg, "maxTextureDimension", 4096);
        if (Math.Max(width, height) > max)
            output.Add(Result("unity.texture-resolution", AssetQaSeverity.Warning,
                "Unity texture exceeds resolution limit", $"Texture is {width}x{height}; project rule allows a maximum dimension of {max}.", o.RelativePath,
                Details(("width", width), ("height", height), ("maxDimension", max))));

        var requireLod = Bool(cfg, "requireLodForMeshes");
        var assetType = String(m, "assetType") ?? string.Empty;
        var lodCount = Int(m, "lodGroupCount");
        if (requireLod && lodCount == 0 && IsMeshAsset(assetType))
            output.Add(Result("unity.lod-required", AssetQaSeverity.Warning,
                "Unity LOD is required", "Project policy requires an LODGroup for mesh/prefab assets.", o.RelativePath,
                Details(("assetType", assetType), ("lodGroupCount", lodCount))));

        var vertices = Int(m, "vertexCount");
        var maxVertices = Int(cfg, "maxMeshVertices", 250000);
        if (vertices > maxVertices)
            output.Add(Result("unity.mesh-vertex-budget", AssetQaSeverity.Warning,
                "Unity mesh vertex budget exceeded", $"Mesh contains {vertices:N0} vertices; project rule allows {maxVertices:N0}.", o.RelativePath,
                Details(("vertexCount", vertices), ("maxMeshVertices", maxVertices))));

        if (Bool(m, "textureReadWriteEnabled"))
            output.Add(Result("unity.texture-readwrite", Severity(cfg, "readWriteTextureSeverity", AssetQaSeverity.Warning),
                "Unity texture Read/Write is enabled", "Read/Write duplicates texture memory on supported platforms; verify the runtime requirement.", o.RelativePath,
                Details(("textureReadWriteEnabled", true))));

        if (Bool(cfg, "requireTextureCompression")
            && String(m, "textureCompression") is { Length: > 0 } compression
            && compression.Equals("Uncompressed", StringComparison.OrdinalIgnoreCase))
            output.Add(Result("unity.texture-compression", AssetQaSeverity.Warning,
                "Unity texture is uncompressed", "Project rules require compressed textures for production assets.", o.RelativePath,
                Details(("textureCompression", compression))));

        var audioMb = Decimal(m, "audioClipSizeMb");
        var audioLimit = Decimal(cfg, "oversizedAudioClipMb", 64m);
        if (audioMb > audioLimit)
            output.Add(Result("unity.audio-memory", AssetQaSeverity.Warning,
                "Unity audio clip is unusually large", $"Audio clip is {audioMb:0.##} MiB; project warning threshold is {audioLimit:0.##} MiB.", o.RelativePath,
                Details(("audioClipSizeMb", audioMb), ("warningThresholdMb", audioLimit))));
    }

    private static void EvaluateUnreal(ProductionToolObservation o, JsonElement m, JsonElement c, List<AssetQaRuleResult> output)
    {
        var cfg = Child(c, "unreal");
        var missing = Int(m, "missingDependencyCount");
        if (missing > 0)
            output.Add(Result("unreal.missing-dependency", Severity(cfg, "missingDependencySeverity", AssetQaSeverity.Critical),
                "Missing Unreal dependency", $"Asset reports {missing} missing dependency reference(s).", o.RelativePath,
                Details(("missingDependencyCount", missing))));

        if (Bool(m, "isRedirector"))
            output.Add(Result("unreal.redirector", Severity(cfg, "redirectorSeverity", AssetQaSeverity.Warning),
                "Unreal redirector requires cleanup", "Asset is a redirector; fix redirectors before release packaging.", o.RelativePath,
                Details(("assetClass", String(m, "assetClass")))));

        var blueprintErrors = Int(m, "blueprintCompileErrorCount");
        if (blueprintErrors > 0)
            output.Add(Result("unreal.blueprint-compile", Severity(cfg, "blueprintCompileSeverity", AssetQaSeverity.Critical),
                "Unreal Blueprint compile errors", $"Asset reports {blueprintErrors} Blueprint compile error(s).", o.RelativePath,
                Details(("blueprintCompileErrorCount", blueprintErrors))));

        var width = Int(m, "textureWidth");
        var height = Int(m, "textureHeight");
        var maxDimension = Int(cfg, "maxTextureDimension", 8192);
        if (Math.Max(width, height) > maxDimension)
            output.Add(Result("unreal.texture-resolution", AssetQaSeverity.Warning,
                "Unreal texture exceeds resolution limit", $"Texture is {width}x{height}; project rule allows a maximum dimension of {maxDimension}.", o.RelativePath,
                Details(("width", width), ("height", height), ("maxDimension", maxDimension))));

        var triangles = Int(m, "triangleCount");
        var naniteThreshold = Int(cfg, "naniteTriangleThreshold", 100000);
        if (Bool(cfg, "requireNaniteAboveThreshold") && triangles > naniteThreshold && !Bool(m, "naniteEnabled"))
            output.Add(Result("unreal.nanite-required", AssetQaSeverity.Warning,
                "Nanite is required for high-density Unreal mesh", $"Mesh contains {triangles:N0} triangles and exceeds the {naniteThreshold:N0} triangle threshold without Nanite enabled.", o.RelativePath,
                Details(("triangleCount", triangles), ("naniteThreshold", naniteThreshold), ("naniteEnabled", false))));

        var instructions = Int(m, "materialInstructionCount");
        var instructionLimit = Int(cfg, "unboundedMaterialInstructionWarning", 800);
        if (instructions > instructionLimit)
            output.Add(Result("unreal.material-complexity", AssetQaSeverity.Warning,
                "Unreal material instruction count is high", $"Material reports {instructions} instructions; project warning threshold is {instructionLimit}.", o.RelativePath,
                Details(("materialInstructionCount", instructions), ("warningThreshold", instructionLimit))));
    }

    private static bool IsMeshAsset(string assetType)
        => assetType.Contains("GameObject", StringComparison.OrdinalIgnoreCase)
           || assetType.Contains("Mesh", StringComparison.OrdinalIgnoreCase)
           || assetType.Contains("Prefab", StringComparison.OrdinalIgnoreCase);

    private static AssetQaRuleResult Result(
        string id,
        AssetQaSeverity severity,
        string title,
        string message,
        string? path,
        IReadOnlyDictionary<string, object?> details)
        => new(id, severity, title, message, path, details);

    private static IReadOnlyDictionary<string, object?> Details(params (string Key, object? Value)[] values)
        => values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static JsonDocument ParseConfig(string json)
    {
        try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); }
        catch (JsonException) { return JsonDocument.Parse("{}"); }
    }

    private static JsonElement Child(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;

    private static int Int(JsonElement root, string name, int fallback = 0)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    private static decimal Decimal(JsonElement root, string name, decimal fallback = 0m)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number) ? number : fallback;

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
