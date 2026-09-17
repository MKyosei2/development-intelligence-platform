// Purpose: Maintains the development graph with fail-closed evidence ingestion and transaction-bounded updates.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

public sealed class DevelopmentGraphService
{
    private const int MaxFilesPerVersionControlChange = 5000;

    private readonly IDevelopmentGraphRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public DevelopmentGraphService(
        IDevelopmentGraphRepository repo,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _repo = repo;
        _uow = uow;
        _clock = clock;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertNodeAsync(
        UpsertDevelopmentGraphNodeCommand command,
        CancellationToken ct)
    {
        var node = await UpsertNodeCoreAsync(command, null, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(node);
    }

    public async Task<DevelopmentGraphEdgeDto> LinkAsync(
        LinkDevelopmentGraphNodesCommand command,
        CancellationToken ct)
    {
        var source = await _repo.GetNodeAsync(command.SourceNodeId, ct)
            ?? throw new KeyNotFoundException(
                $"Graph node {command.SourceNodeId} was not found.");
        var target = await _repo.GetNodeAsync(command.TargetNodeId, ct)
            ?? throw new KeyNotFoundException(
                $"Graph node {command.TargetNodeId} was not found.");

        var edge = await LinkCoreAsync(command, source, target, null, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(edge);
    }

    public async Task<UniversalGraphNodeDto> UpsertUniversalNodeAsync(
        UpsertUniversalGraphNodeCommand command,
        CancellationToken ct)
    {
        if (command.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId is required.", nameof(command));
        }

        var domain = Identifier(command.DomainId, 120, nameof(command.DomainId));
        var kind = Identifier(command.Kind, 160, nameof(command.Kind));
        var schema = Identifier(command.SchemaVersion, 40, nameof(command.SchemaVersion));
        var sourceObjectId = Optional(
            command.SourceObjectId,
            500,
            nameof(command.SourceObjectId));

        var metadata = MergeMetadata(
            command.MetadataJson,
            new Dictionary<string, string?>
            {
                ["domainId"] = domain,
                ["kind"] = kind,
                ["schemaVersion"] = schema,
                ["sourceObjectId"] = sourceObjectId
            });

        var node = await UpsertNodeAsync(
            new(
                command.ProjectId,
                command.FallbackType,
                command.Key,
                command.Name,
                command.Status,
                command.Provider,
                command.ExternalReference,
                metadata),
            ct);

        return new(
            node,
            domain,
            kind,
            schema,
            sourceObjectId);
    }

    public async Task<UniversalGraphEdgeDto> LinkUniversalAsync(
        LinkUniversalGraphNodesCommand command,
        CancellationToken ct)
    {
        if (command.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId is required.", nameof(command));
        }

        var relation = Identifier(
            command.RelationKind,
            160,
            nameof(command.RelationKind));

        var metadata = MergeMetadata(
            command.MetadataJson,
            new Dictionary<string, string?>
            {
                ["relationKind"] = relation
            });

        var edge = await LinkAsync(
            new(
                command.ProjectId,
                command.SourceNodeId,
                command.TargetNodeId,
                command.FallbackType,
                command.Weight,
                command.Critical,
                metadata),
            ct);

        return new(edge, relation);
    }

    public async Task<UniversalGraphDto> GetUniversalAsync(
        Guid projectId,
        string? domainId,
        string? kind,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        }

        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
        var universal = new List<UniversalGraphNodeDto>();

        foreach (var entity in nodes)
        {
            var dto = Map(entity);
            var context = $"development-graph node {entity.Id}";
            var domain = MetadataString(dto.MetadataJson, "domainId", context);
            var objectKind = MetadataString(dto.MetadataJson, "kind", context);

            if (string.IsNullOrWhiteSpace(domain)
                || string.IsNullOrWhiteSpace(objectKind))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(domainId)
                && !domain.Equals(domainId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(kind)
                && !objectKind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            universal.Add(
                new(
                    dto,
                    domain,
                    objectKind,
                    MetadataString(dto.MetadataJson, "schemaVersion", context) ?? "1",
                    MetadataString(dto.MetadataJson, "sourceObjectId", context)));
        }

        var ids = universal.Select(x => x.Node.Id).ToHashSet();
        var edges = new List<UniversalGraphEdgeDto>();

        foreach (var entity in await _repo.ListEdgesAsync(projectId, ct))
        {
            // A filtered graph must never expose an edge whose endpoint is absent.
            if (!ids.Contains(entity.SourceNodeId)
                || !ids.Contains(entity.TargetNodeId))
            {
                continue;
            }

            var dto = Map(entity);
            var relation = MetadataString(
                dto.MetadataJson,
                "relationKind",
                $"development-graph edge {entity.Id}");

            if (!string.IsNullOrWhiteSpace(relation))
            {
                edges.Add(new(dto, relation));
            }
        }

        return new(
            projectId,
            universal
                .OrderBy(x => x.DomainId)
                .ThenBy(x => x.Kind)
                .ThenBy(x => x.Node.Name)
                .ToArray(),
            edges.ToArray());
    }

    public async Task<DevelopmentGraphDto> GetAsync(
        Guid projectId,
        DevelopmentGraphNodeType? type,
        CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, type, ct);
        var ids = nodes.Select(x => x.Id).ToHashSet();

        var edges = (await _repo.ListEdgesAsync(projectId, ct))
            .Where(
                x => ids.Contains(x.SourceNodeId)
                     && ids.Contains(x.TargetNodeId))
            .Select(Map)
            .ToArray();

        return new(
            projectId,
            nodes.Select(Map).ToArray(),
            edges);
    }

    public async Task IngestVersionControlChangeAsync(
        NormalizedVersionControlChange change,
        CancellationToken ct)
    {
        if (change.Files.Count > MaxFilesPerVersionControlChange)
        {
            throw new InvalidDataException(
                $"Version-control evidence contains {change.Files.Count} files, "
                + $"which exceeds the fail-closed limit of {MaxFilesPerVersionControlChange}. "
                + "The event was rejected instead of silently truncating evidence.");
        }

        var nodes = new Dictionary<(DevelopmentGraphNodeType Type, string Key), DevelopmentGraphNode>();
        var edges = new Dictionary<
            (Guid Source, Guid Target, DevelopmentGraphEdgeType Type),
            DevelopmentGraphEdge>();

        var repository = await UpsertNodeCoreAsync(
            new(
                change.ProjectId,
                DevelopmentGraphNodeType.Repository,
                $"repo:{change.RepositoryKey}",
                change.RepositoryKey,
                "Active",
                change.Provider.ToString(),
                change.RepositoryLocator,
                JsonSerializer.Serialize(new { change.RepositoryLocator })),
            nodes,
            ct);

        var branch = await UpsertNodeCoreAsync(
            new(
                change.ProjectId,
                DevelopmentGraphNodeType.Branch,
                $"branch:{change.RepositoryKey}:{change.Branch}",
                change.Branch,
                "Active",
                change.Provider.ToString(),
                null,
                JsonSerializer.Serialize(new { change.RepositoryKey })),
            nodes,
            ct);

        await LinkCoreAsync(
            new(
                change.ProjectId,
                repository.Id,
                branch.Id,
                DevelopmentGraphEdgeType.Contains,
                70m,
                false,
                "{}"),
            repository,
            branch,
            edges,
            ct);

        var commit = await UpsertNodeCoreAsync(
            new(
                change.ProjectId,
                DevelopmentGraphNodeType.Commit,
                $"commit:{change.RepositoryKey}:{change.Revision}",
                change.Title,
                string.IsNullOrWhiteSpace(change.ReleaseTag)
                    ? "Observed"
                    : "Released",
                change.Provider.ToString(),
                change.Url,
                JsonSerializer.Serialize(
                    new
                    {
                        change.Revision,
                        change.Branch,
                        change.IsMerge,
                        change.ReleaseTag
                    })),
            nodes,
            ct);

        await LinkCoreAsync(
            new(
                change.ProjectId,
                branch.Id,
                commit.Id,
                DevelopmentGraphEdgeType.Contains,
                80m,
                false,
                "{}"),
            branch,
            commit,
            edges,
            ct);

        foreach (var file in change.Files)
        {
            var fileNode = await UpsertNodeCoreAsync(
                new(
                    change.ProjectId,
                    DevelopmentGraphNodeType.File,
                    $"file:{change.RepositoryKey}:{Normalize(file.Path)}",
                    file.Path,
                    file.Status,
                    change.Provider.ToString(),
                    null,
                    JsonSerializer.Serialize(
                        new
                        {
                            file.Status,
                            file.Additions,
                            file.Deletions
                        })),
                nodes,
                ct);

            await LinkCoreAsync(
                new(
                    change.ProjectId,
                    commit.Id,
                    fileNode.Id,
                    DevelopmentGraphEdgeType.Changes,
                    Math.Clamp(
                        30m + (file.Additions + file.Deletions) / 10m,
                        30m,
                        100m),
                    false,
                    "{}"),
                commit,
                fileNode,
                edges,
                ct);

            var componentKey = InferComponent(file.Path);
            if (componentKey is null)
            {
                continue;
            }

            var component = await UpsertNodeCoreAsync(
                new(
                    change.ProjectId,
                    DevelopmentGraphNodeType.Component,
                    $"component:{change.RepositoryKey}:{componentKey}",
                    componentKey,
                    "Active",
                    change.Provider.ToString(),
                    null,
                    "{}"),
                nodes,
                ct);

            await LinkCoreAsync(
                new(
                    change.ProjectId,
                    fileNode.Id,
                    component.Id,
                    DevelopmentGraphEdgeType.BelongsTo,
                    70m,
                    false,
                    "{}"),
                fileNode,
                component,
                edges,
                ct);
        }

        if (change.PullRequestNumber.HasValue)
        {
            var pullRequest = await UpsertNodeCoreAsync(
                new(
                    change.ProjectId,
                    DevelopmentGraphNodeType.PullRequest,
                    $"pr:{change.RepositoryKey}:{change.PullRequestNumber.Value}",
                    $"PR #{change.PullRequestNumber.Value}: {change.Title}",
                    "Open",
                    change.Provider.ToString(),
                    change.Url,
                    JsonSerializer.Serialize(
                        new { change.PullRequestNumber })),
                nodes,
                ct);

            await LinkCoreAsync(
                new(
                    change.ProjectId,
                    pullRequest.Id,
                    commit.Id,
                    DevelopmentGraphEdgeType.Contains,
                    85m,
                    false,
                    "{}"),
                pullRequest,
                commit,
                edges,
                ct);

            foreach (var review in change.Reviews ?? Array.Empty<NormalizedReview>())
            {
                var key =
                    $"review:{change.RepositoryKey}:{change.PullRequestNumber.Value}:{review.Reviewer}";

                var reviewNode = await UpsertNodeCoreAsync(
                    new(
                        change.ProjectId,
                        DevelopmentGraphNodeType.Review,
                        key,
                        review.Reviewer,
                        review.State,
                        change.Provider.ToString(),
                        change.Url,
                        JsonSerializer.Serialize(review)),
                    nodes,
                    ct);

                await LinkCoreAsync(
                    new(
                        change.ProjectId,
                        reviewNode.Id,
                        pullRequest.Id,
                        DevelopmentGraphEdgeType.Reviews,
                        75m,
                        false,
                        "{}"),
                    reviewNode,
                    pullRequest,
                    edges,
                    ct);
            }
        }

        if (!string.IsNullOrWhiteSpace(change.ReleaseTag))
        {
            var release = await UpsertNodeCoreAsync(
                new(
                    change.ProjectId,
                    DevelopmentGraphNodeType.Release,
                    $"release:{change.RepositoryKey}:{change.ReleaseTag}",
                    change.ReleaseTag!,
                    "Candidate",
                    change.Provider.ToString(),
                    change.Url,
                    JsonSerializer.Serialize(
                        new
                        {
                            change.ReleaseTag,
                            change.Revision
                        })),
                nodes,
                ct);

            await LinkCoreAsync(
                new(
                    change.ProjectId,
                    release.Id,
                    commit.Id,
                    DevelopmentGraphEdgeType.Targets,
                    100m,
                    true,
                    "{}"),
                release,
                commit,
                edges,
                ct);
        }

        // One unit-of-work commit keeps this evidence event transaction-bounded.
        await _uow.SaveChangesAsync(ct);
    }

    public async Task IngestCiRunAsync(
        NormalizedCiRun run,
        CancellationToken ct)
    {
        var nodes = new Dictionary<(DevelopmentGraphNodeType Type, string Key), DevelopmentGraphNode>();
        var edges = new Dictionary<
            (Guid Source, Guid Target, DevelopmentGraphEdgeType Type),
            DevelopmentGraphEdge>();

        var ci = await UpsertNodeCoreAsync(
            new(
                run.ProjectId,
                DevelopmentGraphNodeType.CiRun,
                $"ci:{run.Provider}:{run.PipelineKey}:{run.RunId}",
                $"{run.PipelineKey} #{run.RunId}",
                run.Status,
                run.Provider.ToString(),
                run.Url,
                JsonSerializer.Serialize(
                    new
                    {
                        run.Revision,
                        run.Branch,
                        run.PassedTests,
                        run.FailedTests,
                        run.SkippedTests,
                        run.MetadataJson
                    })),
            nodes,
            ct);

        if (!string.IsNullOrWhiteSpace(run.Revision))
        {
            var commit = await _repo.FindNodeAsync(
                run.ProjectId,
                DevelopmentGraphNodeType.Commit,
                $"commit:{run.RepositoryKey}:{run.Revision}",
                ct);

            if (commit is not null)
            {
                await LinkCoreAsync(
                    new(
                        run.ProjectId,
                        commit.Id,
                        ci.Id,
                        DevelopmentGraphEdgeType.BuiltBy,
                        90m,
                        run.FailedTests > 0,
                        "{}"),
                    commit,
                    ci,
                    edges,
                    ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
    }

    public Task<DevelopmentGraphNode?> FindNodeAsync(
        Guid projectId,
        DevelopmentGraphNodeType type,
        string key,
        CancellationToken ct) =>
        _repo.FindNodeAsync(projectId, type, key, ct);

    public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(
        Guid projectId,
        DevelopmentGraphNodeType? type,
        CancellationToken ct) =>
        _repo.ListNodesAsync(projectId, type, ct);

    public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(
        Guid projectId,
        CancellationToken ct) =>
        _repo.ListEdgesAsync(projectId, ct);

    private async Task<DevelopmentGraphNode> UpsertNodeCoreAsync(
        UpsertDevelopmentGraphNodeCommand command,
        Dictionary<(DevelopmentGraphNodeType Type, string Key), DevelopmentGraphNode>? cache,
        CancellationToken ct)
    {
        var cacheKey = (command.Type, command.Key);

        if (cache is not null
            && cache.TryGetValue(cacheKey, out var cached))
        {
            cached.Observe(
                command.Name,
                command.Status,
                command.Provider,
                command.ExternalReference,
                Json(command.MetadataJson),
                _clock.UtcNow);
            return cached;
        }

        var node = await _repo.FindNodeAsync(
            command.ProjectId,
            command.Type,
            command.Key,
            ct);

        if (node is null)
        {
            node = new DevelopmentGraphNode(
                Guid.NewGuid(),
                command.ProjectId,
                command.Type,
                command.Key,
                command.Name,
                command.Status,
                command.Provider,
                command.ExternalReference,
                Json(command.MetadataJson),
                _clock.UtcNow);

            await _repo.AddNodeAsync(node, ct);
        }
        else
        {
            node.Observe(
                command.Name,
                command.Status,
                command.Provider,
                command.ExternalReference,
                Json(command.MetadataJson),
                _clock.UtcNow);
        }

        if (cache is not null)
        {
            cache[cacheKey] = node;
        }

        return node;
    }

    private async Task<DevelopmentGraphEdge> LinkCoreAsync(
        LinkDevelopmentGraphNodesCommand command,
        DevelopmentGraphNode source,
        DevelopmentGraphNode target,
        Dictionary<
            (Guid Source, Guid Target, DevelopmentGraphEdgeType Type),
            DevelopmentGraphEdge>? cache,
        CancellationToken ct)
    {
        if (source.ProjectId != command.ProjectId
            || target.ProjectId != command.ProjectId)
        {
            throw new InvalidOperationException(
                "Graph edge endpoints must belong to the requested project.");
        }

        var cacheKey = (
            command.SourceNodeId,
            command.TargetNodeId,
            command.Type);

        if (cache is not null
            && cache.TryGetValue(cacheKey, out var cached))
        {
            cached.Observe(
                command.Weight,
                command.Critical,
                Json(command.MetadataJson),
                _clock.UtcNow);
            return cached;
        }

        var edge = await _repo.FindEdgeAsync(
            command.ProjectId,
            command.SourceNodeId,
            command.TargetNodeId,
            command.Type,
            ct);

        if (edge is null)
        {
            edge = new DevelopmentGraphEdge(
                Guid.NewGuid(),
                command.ProjectId,
                command.SourceNodeId,
                command.TargetNodeId,
                command.Type,
                command.Weight,
                command.Critical,
                Json(command.MetadataJson),
                _clock.UtcNow);

            await _repo.AddEdgeAsync(edge, ct);
        }
        else
        {
            edge.Observe(
                command.Weight,
                command.Critical,
                Json(command.MetadataJson),
                _clock.UtcNow);
        }

        if (cache is not null)
        {
            cache[cacheKey] = edge;
        }

        return edge;
    }

    private static string? InferComponent(string path)
    {
        var parts = Normalize(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return null;
        }

        if (parts[0].Equals("src", StringComparison.OrdinalIgnoreCase)
            || parts[0].Equals("tests", StringComparison.OrdinalIgnoreCase)
            || parts[0].Equals("collectors", StringComparison.OrdinalIgnoreCase)
            || parts[0].Equals("services", StringComparison.OrdinalIgnoreCase))
        {
            return $"{parts[0]}/{parts[1]}";
        }

        return parts.Length >= 3
               && parts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase)
            ? $"Assets/{parts[1]}"
            : parts[0];
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim('/');

    private static string Json(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "{}" : value;

        try
        {
            using var _ = JsonDocument.Parse(normalized);
            return normalized;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                "MetadataJson must contain valid JSON.",
                nameof(value),
                ex);
        }
    }

    private static string MergeMetadata(
        string json,
        IReadOnlyDictionary<string, string?> fields)
    {
        JsonNode? parsed;

        try
        {
            parsed = JsonNode.Parse(
                string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                "MetadataJson must contain valid JSON.",
                nameof(json),
                ex);
        }

        var obj = parsed as JsonObject;
        if (obj is null)
        {
            obj = new JsonObject();
            if (parsed is not null)
            {
                obj["payload"] = parsed.DeepClone();
            }
        }

        foreach (var pair in fields)
        {
            if (pair.Value is null)
            {
                obj.Remove(pair.Key);
            }
            else
            {
                obj[pair.Key] = pair.Value;
            }
        }

        return obj.ToJsonString();
    }

    private static string Identifier(
        string value,
        int max,
        string name)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length is < 1 || normalized.Length > max)
        {
            throw new ArgumentException(
                $"{name} must be 1-{max} characters.",
                name);
        }

        if (normalized.Any(
                ch => !(char.IsLetterOrDigit(ch)
                        || ch is '.' or '-' or '_' or ':' or '/')))
        {
            throw new ArgumentException(
                $"{name} contains unsupported characters.",
                name);
        }

        return normalized;
    }

    private static string? Optional(
        string? value,
        int max,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > max)
        {
            throw new ArgumentException(
                $"{name} must be at most {max} characters.",
                name);
        }

        return normalized;
    }

    private static string? MetadataString(
        string json,
        string name,
        string context)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!document.RootElement.TryGetProperty(name, out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Malformed metadata was encountered while reading {context}.",
                ex);
        }
    }

    private static DevelopmentGraphNodeDto Map(
        DevelopmentGraphNode node) =>
        new(
            node.Id,
            node.Type.ToString(),
            node.Key,
            node.Name,
            node.Status,
            node.Provider,
            node.ExternalReference,
            node.MetadataJson,
            node.LastObservedAtUtc);

    private static DevelopmentGraphEdgeDto Map(
        DevelopmentGraphEdge edge) =>
        new(
            edge.Id,
            edge.SourceNodeId,
            edge.TargetNodeId,
            edge.Type.ToString(),
            edge.Weight,
            edge.Critical,
            edge.MetadataJson,
            edge.LastObservedAtUtc);
}
