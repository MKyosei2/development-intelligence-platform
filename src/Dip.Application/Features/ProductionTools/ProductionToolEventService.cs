using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Evidence;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.QualityAutomation.AssetQa;
using Dip.Application.Features.Policies;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProductionTools;

public sealed class ProductionToolEventService
{
    private readonly IProjectRepository _projects;
    private readonly EvidenceIngestionService _evidence;
    private readonly PolicyResolver _policies;
    private readonly ProductionGraphProjectionService _graph;
    private readonly AssetQaService _assetQa;

    public ProductionToolEventService(
        IProjectRepository projects,
        EvidenceIngestionService evidence,
        PolicyResolver policies,
        ProductionGraphProjectionService graph,
        AssetQaService assetQa)
    {
        _projects = projects;
        _evidence = evidence;
        _policies = policies;
        _graph = graph;
        _assetQa = assetQa;
    }

    public async Task<ProductionToolBatchResult> IngestAsync(
        Guid projectId,
        ProductionToolBatchRequest request,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.AgentId)) throw new ArgumentException("AgentId is required.", nameof(request));
        if (request.Events is null) throw new ArgumentException("Events are required.", nameof(request));
        if (request.Events.Count > 250) throw new ArgumentException("At most 250 production-tool events are accepted per batch.", nameof(request));
        if (await _projects.GetByIdAsync(projectId, ct) is null)
            throw new KeyNotFoundException($"Project {projectId} was not found.");

        var agentId = request.AgentId.Trim();
        var policy = await _policies.ResolveAsync(projectId, agentId, ct);
        var results = new List<ProductionToolEventResult>();
        var warnings = new List<string>();
        var duplicates = 0;
        var rejected = 0;

        foreach (var item in request.Events)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                Validate(item);
                if (!SourceAllowed(policy, item.Source))
                {
                    rejected++;
                    warnings.Add($"{item.EventId}: {item.Source} monitoring is disabled by effective policy.");
                    continue;
                }

                var eventType = ProductionToolEventMapping.EventType(item.Source, item.Kind);
                var metadata = policy.Evidence.CollectMetadata
                    ? NormalizeMetadata(item)
                    : null;

                var stored = await _evidence.IngestAsync(
                    new IngestEvidenceCommand(
                        projectId,
                        ProductionToolEventMapping.SourceType(item.Source),
                        $"agent:{agentId}:tool:{item.Source.ToString().ToLowerInvariant()}",
                        item.EventId.Trim(),
                        eventType,
                        SubjectKey(item),
                        ProductionToolEventMapping.EvidenceKind(item.Source, item.Kind),
                        ProductionToolEventMapping.Title(item),
                        ProductionToolEventMapping.Summary(item),
                        EvidenceOutcome.Changed,
                        EvidenceConfidence.High,
                        item.ObservedAtUtc == default ? DateTimeOffset.UtcNow : item.ObservedAtUtc,
                        SourceReference: null,
                        ActorId: null,
                        MachineId: agentId,
                        ArtifactId: NormalizeOptionalPath(item.ArtifactRelativePath ?? item.RelativePath),
                        ContentHash: NormalizeHash(item.ContentHash),
                        PrivacyClass: EvidencePrivacyClass.ProjectRestricted,
                        Metadata: metadata),
                    ct);

                if (!stored.Created) duplicates++;
                if (stored.Created)
                {
                    try
                    {
                        await _graph.ProjectProductionEventAsync(projectId, agentId, item, stored.EvidenceId, ct);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        warnings.Add($"{item.EventId}: graph projection failed: {Safe(exception.Message)}");
                    }
                    try
                    {
                        await _assetQa.EvaluateAsync(projectId, stored.EvidenceId, item, ct);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        warnings.Add($"{item.EventId}: asset QA failed: {Safe(exception.Message)}");
                    }
                }
                results.Add(new ProductionToolEventResult(item.EventId, stored.EvidenceId, stored.Created, eventType));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                rejected++;
                warnings.Add($"{Safe(item.EventId)}: {Safe(exception.Message)}");
            }
        }

        return new ProductionToolBatchResult(
            request.Events.Count,
            results.Count,
            duplicates,
            rejected,
            results,
            warnings);
    }

    private static JsonElement? NormalizeMetadata(ProductionToolObservation item)
    {
        var metadataObject = new Dictionary<string, object?>
        {
            ["source"] = item.Source.ToString(),
            ["eventKind"] = item.Kind.ToString(),
            ["toolVersion"] = item.ToolVersion,
            ["relativePath"] = NormalizeOptionalPath(item.RelativePath),
            ["artifactRelativePath"] = NormalizeOptionalPath(item.ArtifactRelativePath),
            ["sizeBytes"] = item.SizeBytes,
            ["mediaKind"] = string.IsNullOrWhiteSpace(item.MediaRelativePath) ? null : item.MediaKind?.ToString(),
            ["mediaRelativePath"] = NormalizeOptionalPath(item.MediaRelativePath),
            ["mediaContentType"] = item.MediaContentType
        };

        if (item.Metadata is { } custom && custom.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in custom.EnumerateObject())
            {
                if (metadataObject.ContainsKey(property.Name)) continue;
                metadataObject[property.Name] = JsonSerializer.Deserialize<object?>(property.Value.GetRawText());
            }
        }

        return JsonSerializer.SerializeToElement(metadataObject);
    }

    private static bool SourceAllowed(EffectivePolicy policy, ProductionToolSource source) => source switch
    {
        ProductionToolSource.Maya => policy.Agent.MayaMonitoringEnabled,
        ProductionToolSource.Unity => policy.Agent.UnityMonitoringEnabled,
        ProductionToolSource.Unreal => policy.Agent.UnrealMonitoringEnabled,
        _ => false
    };

    private static string SubjectKey(ProductionToolObservation item)
    {
        var target = NormalizeOptionalPath(item.RelativePath)
                     ?? NormalizeOptionalPath(item.MediaRelativePath)
                     ?? "project";
        return $"tool:{item.Source.ToString().ToLowerInvariant()}:{target}"[..Math.Min(200, $"tool:{item.Source.ToString().ToLowerInvariant()}:{target}".Length)];
    }

    private static void Validate(ProductionToolObservation item)
    {
        if (string.IsNullOrWhiteSpace(item.EventId) || item.EventId.Trim().Length > 300)
            throw new ArgumentException("EventId must be 1-300 characters.");
        if (!Enum.IsDefined(item.Source) || item.Source == ProductionToolSource.Unknown)
            throw new ArgumentException("A supported production-tool source is required.");
        if (!Enum.IsDefined(item.Kind) || item.Kind == ProductionToolEventKind.Unknown)
            throw new ArgumentException("A supported production-tool event kind is required.");
        if (item.SizeBytes is < 0) throw new ArgumentOutOfRangeException(nameof(item.SizeBytes));
        _ = NormalizeHash(item.ContentHash);
        _ = NormalizeOptionalPath(item.RelativePath);
        _ = NormalizeOptionalPath(item.ArtifactRelativePath);
        _ = NormalizeOptionalPath(item.MediaRelativePath);
        if (item.Metadata is { } metadata && metadata.GetRawText().Length > 24000)
            throw new ArgumentException("Production tool metadata is too large.");
    }

    internal static string? NormalizeOptionalPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Length > 1000) throw new ArgumentException("Project-relative path is too long.");
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(x => x is "." or ".."))
            throw new ArgumentException("Paths must be project-relative.");
        return normalized;
    }

    internal static string? NormalizeHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hash = value.Trim().ToLowerInvariant();
        if (hash.Length != 64 || hash.Any(ch => !Uri.IsHexDigit(ch)))
            throw new ArgumentException("ContentHash must be a 64-character SHA-256 hexadecimal value.");
        return hash;
    }

    private static string Safe(string? value)
    {
        var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized[..Math.Min(1000, normalized.Length)];
    }
}
