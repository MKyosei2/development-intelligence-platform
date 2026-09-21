// Purpose: Evaluates deterministic asset-pipeline quality and dependency risk signals with one unit-of-work commit per observation.
using System.IO;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Assets;

public sealed class AssetIntelligenceService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly IP2IntelligenceRepository _records;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public AssetIntelligenceService(
        DevelopmentGraphService graph,
        IDevelopmentGraphRepository repo,
        IP2IntelligenceRepository records,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _records = records;
        _uow = uow;
        _clock = clock;
    }

    public async Task<AssetIntelligenceResult> ObserveAsync(
        AssetObservationCommand command,
        CancellationToken ct)
    {
        var issues = (command.Issues ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        decimal score = 0m;
        if (!command.ImportSucceeded)
        {
            score += 45m;
            issues.Add("Asset import failed.");
        }
        if (command.MissingReference)
        {
            score += 40m;
            issues.Add("Asset has a missing/broken reference.");
        }
        if (command.PlatformIncompatible)
        {
            score += 35m;
            issues.Add("Asset is incompatible with at least one target platform.");
        }
        if (command.SizeBytes > 256L * 1024 * 1024)
        {
            score += 30m;
            issues.Add($"Very large asset: {command.SizeBytes / 1024d / 1024d:0.#} MiB.");
        }
        else if (command.SizeBytes > 64L * 1024 * 1024)
        {
            score += 12m;
            issues.Add($"Large asset: {command.SizeBytes / 1024d / 1024d:0.#} MiB.");
        }

        var all = await _repo.ListNodesAsync(command.ProjectId, DevelopmentGraphNodeType.Asset, ct);
        var duplicate = !string.IsNullOrWhiteSpace(command.ContentHash)
            && all.Any(node =>
                !node.Key.Equals(Key(command.RepositoryKey, command.Path), StringComparison.OrdinalIgnoreCase)
                && string.Equals(ReadHash(node.MetadataJson), command.ContentHash, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            score += 15m;
            issues.Add("Another asset node has the same content hash; duplicate content may be present.");
        }

        score = Math.Clamp(decimal.Round(score + Math.Min(20m, issues.Count * 2m), 2), 0m, 100m);
        var state = score >= 80m
            ? "Critical"
            : score >= 50m
                ? "HighRisk"
                : score >= 20m
                    ? "Warning"
                    : "Healthy";
        var key = Key(command.RepositoryKey, command.Path);

        var commit = string.IsNullOrWhiteSpace(command.CommitSha)
            ? null
            : await _repo.FindNodeAsync(
                command.ProjectId,
                DevelopmentGraphNodeType.Commit,
                $"commit:{command.RepositoryKey}:{command.CommitSha}",
                ct);

        await _graph.StageMutationAsync(
            async (graph, token) =>
            {
                var assetNode = await graph.UpsertNodeAsync(
                    new(
                        command.ProjectId,
                        DevelopmentGraphNodeType.Asset,
                        key,
                        command.Path,
                        state,
                        "AssetPipeline",
                        null,
                        JsonSerializer.Serialize(new
                        {
                            command.RepositoryKey,
                            command.Path,
                            command.AssetType,
                            command.SizeBytes,
                            command.ContentHash,
                            command.CommitSha,
                            command.Platform,
                            command.ImportSucceeded,
                            command.MissingReference,
                            command.PlatformIncompatible,
                            Issues = issues
                        })),
                    token);

                foreach (var dependencyPath in command.Dependencies ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(dependencyPath))
                        continue;

                    var dependency = await graph.UpsertNodeAsync(
                        new(
                            command.ProjectId,
                            DevelopmentGraphNodeType.Asset,
                            Key(command.RepositoryKey, dependencyPath),
                            dependencyPath,
                            "Observed",
                            "AssetPipeline",
                            null,
                            JsonSerializer.Serialize(new
                            {
                                RepositoryKey = command.RepositoryKey,
                                Path = dependencyPath
                            })),
                        token);
                    await graph.LinkAsync(
                        new(
                            command.ProjectId,
                            assetNode.Id,
                            dependency.Id,
                            DevelopmentGraphEdgeType.DependsOn,
                            75m,
                            command.MissingReference,
                            JsonSerializer.Serialize(new { kind = "asset-reference" })),
                        token);
                }

                if (commit is not null)
                {
                    await graph.LinkAsync(
                        new(
                            command.ProjectId,
                            commit.Id,
                            assetNode.Id,
                            DevelopmentGraphEdgeType.Changes,
                            80m,
                            state == "Critical",
                            "{}"),
                        token);
                }

                return true;
            },
            ct);

        var actions = new List<string>();
        if (!command.ImportSucceeded)
            actions.Add("Fix import settings/source data and re-import the asset.");
        if (command.MissingReference)
            actions.Add("Repair missing references before the next candidate build.");
        if (command.PlatformIncompatible)
            actions.Add("Validate platform-specific import/format settings.");
        if (duplicate)
            actions.Add("Confirm whether duplicate content is intentional; deduplicate if not.");
        if (score >= 50m)
            actions.Add("Require lead-art/asset-pipeline review before release.");

        var summary =
            $"Asset '{command.Path}' is {state} with risk {score:0.##}/100 "
            + $"and {issues.Count} issue(s).";
        var result = new AssetIntelligenceResult(
            key,
            state,
            score,
            duplicate,
            issues,
            actions.Distinct().ToArray(),
            summary);

        await _records.AddAsync(
            new P2IntelligenceRecord(
                Guid.NewGuid(),
                command.ProjectId,
                key,
                P2IntelligenceKind.AssetIntelligence,
                score >= 50m ? P2IntelligenceStatus.Warning : P2IntelligenceStatus.Informational,
                score,
                EvidenceConfidence.High,
                summary,
                JsonSerializer.Serialize(result),
                null,
                _clock.UtcNow),
            ct);

        await _uow.SaveChangesAsync(ct);
        return result;
    }

    private static string Key(string repository, string path) =>
        $"asset:{repository}:{path.Replace('\\', '/').Trim('/')}";

    private static string? ReadHash(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("ContentHash", out var hash)
                && hash.ValueKind == JsonValueKind.String
                    ? hash.GetString()
                    : null;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Asset metadata is invalid JSON; duplicate-content analysis cannot be trusted.",
                ex);
        }
    }
}
