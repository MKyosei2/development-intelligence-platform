// Purpose: Coordinates P ro du ct io nG ra ph Pr oj ec ti on Se rv ic e application behavior for DevelopmentGraph.
using System.Text.Json;
using Dip.Application.Features.ProductionTools;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

public sealed class ProductionGraphProjectionService
{
    private readonly DevelopmentGraphService _graph;

    public ProductionGraphProjectionService(DevelopmentGraphService graph) => _graph = graph;

    public async Task ProjectProductionEventAsync(
        Guid projectId,
        string agentId,
        ProductionToolObservation observation,
        Guid evidenceId,
        CancellationToken ct)
    {
        var provider = observation.Source.ToString();
        var evidence = await _graph.UpsertUniversalNodeAsync(new(
            projectId,
            "development-evidence",
            "ProductionEvidence",
            $"production-evidence:{evidenceId:N}",
            $"{provider} {observation.Kind}",
            "Observed",
            provider,
            null,
            evidenceId.ToString("N"),
            "phase4.v1",
            DevelopmentGraphNodeType.Evidence,
            JsonSerializer.Serialize(new { observation.EventId, observation.Kind, observation.ObservedAtUtc })), ct);

        var machine = await _graph.UpsertUniversalNodeAsync(new(
            projectId,
            "development-agent",
            "Machine",
            $"machine:{agentId.Trim().ToLowerInvariant()}",
            agentId.Trim(),
            "OnlineOrObserved",
            "DIP Agent",
            null,
            agentId.Trim(),
            "phase4.v1",
            DevelopmentGraphNodeType.ExternalObservation,
            "{}"), ct);
        await _graph.LinkUniversalAsync(new(projectId, machine.Node.Id, evidence.Node.Id, "observed-by", 80m, false, DevelopmentGraphEdgeType.ObservedBy), ct);

        if (!string.IsNullOrWhiteSpace(observation.RelativePath))
        {
            var path = observation.RelativePath.Trim().Replace('\\', '/');
            var kind = observation.Source switch
            {
                ProductionToolSource.Maya => "MayaScene",
                ProductionToolSource.Unity => "UnityAsset",
                ProductionToolSource.Unreal => "UnrealAsset",
                _ => "Asset"
            };
            var asset = await _graph.UpsertUniversalNodeAsync(new(
                projectId,
                "production",
                kind,
                $"asset:{provider.ToLowerInvariant()}:{path.ToLowerInvariant()}",
                path,
                "Observed",
                provider,
                null,
                path,
                "phase4.v1",
                DevelopmentGraphNodeType.Asset,
                JsonSerializer.Serialize(new { observation.ContentHash, observation.SizeBytes, observation.ToolVersion })), ct);
            await _graph.LinkUniversalAsync(new(projectId, evidence.Node.Id, asset.Node.Id, "evidence-for", 90m, false, DevelopmentGraphEdgeType.EvidenceFor), ct);
        }

        if (observation.Kind is ProductionToolEventKind.BuildStarted or ProductionToolEventKind.BuildCompleted
            or ProductionToolEventKind.CookCompleted or ProductionToolEventKind.PackageCompleted)
        {
            var build = await _graph.UpsertUniversalNodeAsync(new(
                projectId, "production", "Build", $"build:{provider.ToLowerInvariant()}:{observation.EventId}",
                $"{provider} {observation.Kind}", observation.Kind == ProductionToolEventKind.BuildStarted ? "Running" : "Observed",
                provider, null, observation.EventId, "phase4.v1", DevelopmentGraphNodeType.Build,
                observation.Metadata?.GetRawText() ?? "{}"), ct);
            await _graph.LinkUniversalAsync(new(projectId, evidence.Node.Id, build.Node.Id, "describes-build", 85m, false, DevelopmentGraphEdgeType.EvidenceFor), ct);
        }

        if (observation.Kind is ProductionToolEventKind.TestsCompleted or ProductionToolEventKind.AutomationTestsCompleted)
        {
            var test = await _graph.UpsertUniversalNodeAsync(new(
                projectId, "production", "TestRun", $"test:{provider.ToLowerInvariant()}:{observation.EventId}",
                $"{provider} tests", "Observed", provider, null, observation.EventId, "phase4.v1",
                DevelopmentGraphNodeType.Test, observation.Metadata?.GetRawText() ?? "{}"), ct);
            await _graph.LinkUniversalAsync(new(projectId, evidence.Node.Id, test.Node.Id, "verifies", 90m, false, DevelopmentGraphEdgeType.VerifiedBy), ct);
        }
    }

    public async Task ProjectAssetFindingAsync(
        Guid projectId,
        Guid findingId,
        Guid evidenceId,
        string ruleId,
        string title,
        string? relativePath,
        bool critical,
        CancellationToken ct)
    {
        var finding = await _graph.UpsertUniversalNodeAsync(new(
            projectId, "quality", "AssetQaFinding", $"asset-qa:{findingId:N}", title,
            critical ? "Critical" : "Open", "DIP Asset QA", null, findingId.ToString("N"), "phase4.v1",
            DevelopmentGraphNodeType.QaFinding, JsonSerializer.Serialize(new { evidenceId, ruleId, relativePath })), ct);
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            var asset = await _graph.UpsertUniversalNodeAsync(new(
                projectId, "production", "Asset", $"asset:any:{relativePath.Trim().Replace('\\','/').ToLowerInvariant()}",
                relativePath, "Observed", "DIP", null, relativePath, "phase4.v1", DevelopmentGraphNodeType.Asset, "{}"), ct);
            await _graph.LinkUniversalAsync(new(projectId, finding.Node.Id, asset.Node.Id, "finding-for", 95m, critical, DevelopmentGraphEdgeType.Affects), ct);
        }
    }

    public async Task ProjectVisualComparisonAsync(
        Guid projectId,
        Guid comparisonId,
        string captureKey,
        VisualComparisonVerdict verdict,
        Guid currentArtifactRecordId,
        Guid? diffArtifactRecordId,
        CancellationToken ct)
    {
        var node = await _graph.UpsertUniversalNodeAsync(new(
            projectId, "quality", "VisualComparison", $"visual-comparison:{comparisonId:N}",
            $"Visual comparison: {captureKey}", verdict.ToString(), "DIP Visual QA",
            diffArtifactRecordId.HasValue ? $"/api/v1/artifacts/{diffArtifactRecordId.Value}" : null,
            comparisonId.ToString("N"), "phase4.v1", DevelopmentGraphNodeType.QaFinding,
            JsonSerializer.Serialize(new { currentArtifactRecordId, diffArtifactRecordId, verdict = verdict.ToString() })), ct);
        var asset = await _graph.UpsertUniversalNodeAsync(new(
            projectId, "production", "VisualCapture", $"visual-capture:{captureKey.ToLowerInvariant()}",
            captureKey, "Observed", "DIP", null, captureKey, "phase4.v1", DevelopmentGraphNodeType.Asset, "{}"), ct);
        await _graph.LinkUniversalAsync(new(projectId, node.Node.Id, asset.Node.Id, "visual-verification-of", 100m,
            verdict == VisualComparisonVerdict.Failed, DevelopmentGraphEdgeType.VerifiedBy), ct);
    }

    public async Task ProjectClaimAssessmentAsync(
        Guid projectId,
        Guid assessmentId,
        Guid claimId,
        IReadOnlyList<Guid> supportingEvidenceIds,
        IReadOnlyList<Guid> contradictingEvidenceIds,
        CancellationToken ct)
    {
        var claim = await _graph.UpsertUniversalNodeAsync(new(
            projectId, "management", "Claim", $"claim:{claimId:N}", $"Claim {claimId:N}", "Assessed",
            "DIP", null, claimId.ToString("N"), "phase4.v1", DevelopmentGraphNodeType.WorkItem, "{}"), ct);
        var assessment = await _graph.UpsertUniversalNodeAsync(new(
            projectId, "management", "ClaimAssessment", $"claim-assessment:{assessmentId:N}",
            $"Assessment {assessmentId:N}", "Observed", "DIP Claim Verification", null,
            assessmentId.ToString("N"), "phase4.v1", DevelopmentGraphNodeType.Evidence, "{}"), ct);
        await _graph.LinkUniversalAsync(new(projectId, assessment.Node.Id, claim.Node.Id, "assesses", 100m, false, DevelopmentGraphEdgeType.EvidenceFor), ct);

        foreach (var id in supportingEvidenceIds.Distinct())
        {
            var ev = await _graph.UpsertUniversalNodeAsync(new(
                projectId, "development-evidence", "Evidence", $"evidence:{id:N}", $"Evidence {id:N}", "Observed",
                "DIP", null, id.ToString("N"), "phase4.v1", DevelopmentGraphNodeType.Evidence, "{}"), ct);
            await _graph.LinkUniversalAsync(new(projectId, ev.Node.Id, claim.Node.Id, "supports", 90m, false, DevelopmentGraphEdgeType.EvidenceFor), ct);
        }
        foreach (var id in contradictingEvidenceIds.Distinct())
        {
            var ev = await _graph.UpsertUniversalNodeAsync(new(
                projectId, "development-evidence", "Evidence", $"evidence:{id:N}", $"Evidence {id:N}", "Observed",
                "DIP", null, id.ToString("N"), "phase4.v1", DevelopmentGraphNodeType.Evidence, "{}"), ct);
            await _graph.LinkUniversalAsync(new(projectId, ev.Node.Id, claim.Node.Id, "contradicts", 100m, true, DevelopmentGraphEdgeType.Contradicts), ct);
        }
    }
}
