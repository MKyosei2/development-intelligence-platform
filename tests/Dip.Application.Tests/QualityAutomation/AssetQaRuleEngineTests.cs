// Purpose: Verifies behavior and invariants covered by A ss et QA Ru le En gi ne Te st s.
using System.Text.Json;
using Dip.Application.Features.ProductionTools;
using Dip.Application.Features.QualityAutomation.AssetQa;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class AssetQaRuleEngineTests
{
    [Fact]
    public void Maya_metadata_produces_missing_texture_unknown_node_and_budget_findings()
    {
        var metadata = JsonSerializer.SerializeToElement(new
        {
            polygonCount = 900000,
            missingTextures = new[] { "textures/missing.png" },
            unknownNodeCount = 2
        });
        var observation = new ProductionToolObservation(
            "maya:1", ProductionToolSource.Maya, ProductionToolEventKind.SceneSaved, "2026",
            "Scenes/hero.ma", "Scenes/hero.ma", null, 100, DateTimeOffset.UtcNow, metadata);
        var engine = new AssetQaRuleEngine();
        var findings = engine.Evaluate(observation, """{"maya":{"polygonBudget":500000,"missingTextureSeverity":"Critical","unknownNodeSeverity":"Warning"}}""");
        findings.Select(x => x.RuleId).Should().Contain(new[] { "maya.missing-texture", "maya.unknown-node", "maya.polygon-budget" });
        findings.Single(x => x.RuleId == "maya.missing-texture").Severity.Should().Be(AssetQaSeverity.Critical);
    }

    [Fact]
    public void Unity_texture_and_missing_script_rules_are_deterministic()
    {
        var metadata = JsonSerializer.SerializeToElement(new { assetType = "GameObject", textureWidth = 8192, textureHeight = 4096, missingScriptCount = 1, lodGroupCount = 0 });
        var observation = new ProductionToolObservation(
            "unity:1", ProductionToolSource.Unity, ProductionToolEventKind.AssetImported, "6000",
            "Assets/Hero.prefab", "Assets/Hero.prefab", null, 100, DateTimeOffset.UtcNow, metadata);
        var engine = new AssetQaRuleEngine();
        var findings = engine.Evaluate(observation, """{"unity":{"maxTextureDimension":4096,"missingScriptSeverity":"Critical","requireLodForMeshes":true}}""");
        findings.Select(x => x.RuleId).Should().Contain(new[] { "unity.texture-resolution", "unity.missing-script", "unity.lod-required" });
    }
}
