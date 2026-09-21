// Purpose: Coordinates milestone persistence and delegates deterministic critical-path calculation to a pure evaluator.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Milestones;

public sealed record UpsertMilestoneCommand(
    Guid ProjectId,
    string Key,
    string Name,
    DateTimeOffset PlannedStartUtc,
    DateTimeOffset DueAtUtc,
    int DurationDays,
    decimal CompletionPercent = 0m,
    MilestoneState State = MilestoneState.Planned,
    string? ExternalReference = null);

public sealed record MilestoneDependencyCommand(
    Guid ProjectId,
    string PredecessorKey,
    string SuccessorKey,
    bool Critical = true);

public sealed record CriticalPathMilestone(
    string Key,
    string Name,
    int DurationDays,
    int EarliestStartDay,
    int EarliestFinishDay,
    int LatestStartDay,
    int LatestFinishDay,
    int SlackDays,
    bool IsCritical,
    DateTimeOffset DueAtUtc,
    decimal CompletionPercent,
    string State);

public sealed record CriticalPathResult(
    string Status,
    int TotalDurationDays,
    int CriticalMilestones,
    int AtRiskMilestones,
    IReadOnlyList<CriticalPathMilestone> Milestones,
    IReadOnlyList<string> CriticalPath,
    IReadOnlyList<string> Blockers,
    string Summary);

public sealed class MilestoneIntelligenceService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly IP2IntelligenceRepository _records;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public MilestoneIntelligenceService(
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

    public Task<DevelopmentGraphNodeDto> UpsertAsync(
        UpsertMilestoneCommand command,
        CancellationToken ct)
    {
        if (command.DueAtUtc < command.PlannedStartUtc)
        {
            throw new ArgumentException(
                "Milestone due date must not precede its planned start.",
                nameof(command));
        }

        var status = command.State.ToString();
        return _graph.UpsertNodeAsync(
            new(
                command.ProjectId,
                DevelopmentGraphNodeType.Milestone,
                $"milestone:{command.Key}",
                command.Name,
                status,
                "DIP",
                command.ExternalReference,
                JsonSerializer.Serialize(new
                {
                    command.Key,
                    command.PlannedStartUtc,
                    command.DueAtUtc,
                    DurationDays = Math.Clamp(command.DurationDays, 1, 3650),
                    CompletionPercent = Math.Clamp(command.CompletionPercent, 0m, 100m),
                    State = status
                })),
            ct);
    }

    public async Task LinkDependencyAsync(
        MilestoneDependencyCommand command,
        CancellationToken ct)
    {
        var predecessor = await _repo.FindNodeAsync(
            command.ProjectId,
            DevelopmentGraphNodeType.Milestone,
            $"milestone:{command.PredecessorKey}",
            ct)
            ?? throw new KeyNotFoundException($"Milestone '{command.PredecessorKey}' was not found.");
        var successor = await _repo.FindNodeAsync(
            command.ProjectId,
            DevelopmentGraphNodeType.Milestone,
            $"milestone:{command.SuccessorKey}",
            ct)
            ?? throw new KeyNotFoundException($"Milestone '{command.SuccessorKey}' was not found.");

        await _graph.LinkAsync(
            new(
                command.ProjectId,
                predecessor.Id,
                successor.Id,
                DevelopmentGraphEdgeType.Blocks,
                command.Critical ? 100m : 70m,
                command.Critical,
                JsonSerializer.Serialize(new { dependency = "finish-to-start" })),
            ct);
    }

    public async Task<CriticalPathResult> AnalyzeAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.Milestone, ct);
        var ids = nodes.Select(node => node.Id).ToHashSet();
        var dependencies = (await _repo.ListEdgesAsync(projectId, ct))
            .Where(edge =>
                ids.Contains(edge.SourceNodeId)
                && ids.Contains(edge.TargetNodeId)
                && edge.Type is DevelopmentGraphEdgeType.Blocks or DevelopmentGraphEdgeType.DependsOn)
            .Select(edge => new MilestoneDependencyInput(edge.SourceNodeId, edge.TargetNodeId))
            .ToArray();

        var result = CriticalPathEvaluator.Evaluate(
            nodes.Select(Parse).ToArray(),
            dependencies,
            _clock.UtcNow);

        if (result.Status == "NoMilestones")
            return result;

        var warning = result.Blockers.Count > 0 || result.Status == "CycleDetected";
        var score = result.Status == "CycleDetected"
            ? 85m
            : Math.Clamp(result.Blockers.Count * 25m + result.AtRiskMilestones * 8m, 0m, 100m);
        await Store(
            projectId,
            result,
            warning ? P2IntelligenceStatus.Warning : P2IntelligenceStatus.Informational,
            score,
            ct);
        return result;
    }

    private async Task Store(
        Guid projectId,
        CriticalPathResult result,
        P2IntelligenceStatus status,
        decimal score,
        CancellationToken ct)
    {
        await _records.AddAsync(
            new P2IntelligenceRecord(
                Guid.NewGuid(),
                projectId,
                "project",
                P2IntelligenceKind.MilestoneCriticalPath,
                status,
                score,
                EvidenceConfidence.High,
                result.Summary,
                JsonSerializer.Serialize(result),
                null,
                _clock.UtcNow),
            ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static MilestoneCriticalPathInput Parse(DevelopmentGraphNode node)
    {
        using var document = JsonDocument.Parse(node.MetadataJson);
        var root = document.RootElement;
        return new(
            node.Id,
            root.TryGetProperty("Key", out var key)
                ? key.GetString() ?? node.Key.Replace("milestone:", "")
                : node.Key.Replace("milestone:", ""),
            node.Name,
            root.TryGetProperty("DurationDays", out var duration)
                && duration.TryGetInt32(out var durationDays)
                    ? Math.Max(1, durationDays)
                    : 1,
            root.TryGetProperty("DueAtUtc", out var due)
                && due.TryGetDateTimeOffset(out var dueAtUtc)
                    ? dueAtUtc
                    : DateTimeOffset.MaxValue,
            root.TryGetProperty("CompletionPercent", out var completion)
                && completion.TryGetDecimal(out var completionPercent)
                    ? Math.Clamp(completionPercent, 0m, 100m)
                    : 0m,
            node.Status);
    }
}
