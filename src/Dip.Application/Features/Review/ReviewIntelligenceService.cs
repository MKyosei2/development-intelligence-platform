// Purpose: Coordinates Review Intelligence application responsibilities for review.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Review;

public sealed record ReviewIntelligenceCommand(Guid ProjectId, string SubjectKey, string RepositoryKey, string Revision, int? PullRequestNumber, IReadOnlyList<string> ChangedFiles, int Approvals, int ChangesRequested, int UnresolvedThreads, int RequiredApprovals = 1, bool TestsChanged = false, string? QaStatus = null, bool PublicApiChanged = false, bool SchemaChanged = false);
public sealed record ReviewIntelligenceResult(string State, decimal RiskScore, IReadOnlyList<string> Blockers, IReadOnlyList<string> Warnings, IReadOnlyList<string> RequiredReviews, IReadOnlyList<string> Actions, string Summary);

public sealed class ReviewIntelligenceService
{
    private readonly IChangeRiskRepository _changeRisk;
     private readonly IDevelopmentGraphRepository _graph;
     private readonly DevelopmentGraphService _graphService;
     private readonly IP2IntelligenceRepository _records;
     private readonly IUnitOfWork _uow;
     private readonly ISystemClock _clock;
    public ReviewIntelligenceService(IChangeRiskRepository changeRisk, IDevelopmentGraphRepository graph, DevelopmentGraphService graphService, IP2IntelligenceRepository records, IUnitOfWork uow, ISystemClock clock) { _changeRisk = changeRisk;
     _graph = graph;
     _graphService = graphService;
     _records = records;
     _uow = uow;
     _clock = clock;
     }
    public async Task<ReviewIntelligenceResult> AssessAsync(ReviewIntelligenceCommand c, CancellationToken ct)
    {
        var blockers = new List<string>();
         var warnings = new List<string>();
         var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         var actions = new List<string>();
         decimal score = 0m;
         var latest = await _changeRisk.GetLatestAsync(c.ProjectId, c.Revision, ct);
        if (latest is not null) { score += latest.RiskScore * .45m;
        if (latest.RiskLevel is ChangeRiskLevel.High or ChangeRiskLevel.Critical) required.Add("Lead engineer / subsystem owner");
         }
        if (c.Approvals < c.RequiredApprovals) { blockers.Add($"Only {c.Approvals}/{c.RequiredApprovals} required approval(s) are present.");
         score += 25m;
         actions.Add("Obtain the configured required approvals.");
         }
        if (c.ChangesRequested > 0) { blockers.Add($"{c.ChangesRequested} review(s) currently request changes."); score += 25m; }
        if (c.UnresolvedThreads > 0) { warnings.Add($"{c.UnresolvedThreads} review thread(s) remain unresolved."); score += Math.Min(20m, c.UnresolvedThreads * 3m); }
        if (c.ChangedFiles.Count >= 20) { warnings.Add($"Large review surface: {c.ChangedFiles.Count} changed files."); score += 12m; }
        if (!c.TestsChanged && c.ChangedFiles.Any(IsSource)) { warnings.Add("Source changed without an observed test-file change.");
         score += 12m;
         actions.Add("Run test-impact analysis and document why no new/updated test is required.");
         }
        if (c.PublicApiChanged) { required.Add("API / integration owner"); score += 10m; }
        if (c.SchemaChanged) { required.Add("Data/schema owner"); score += 12m; }
        if (c.ChangedFiles.Any(IsAsset)) { required.Add("Lead artist / asset pipeline owner"); }
        if (string.Equals(c.QaStatus, "Failed", StringComparison.OrdinalIgnoreCase)) { blockers.Add("QA status is Failed.");
         score += 30m;
         actions.Add("Resolve QA failures before merge.");
         }
        score = Math.Clamp(decimal.Round(score, 2), 0m, 100m);
         var state = blockers.Count > 0 ? "Blocked" : score >= 65m ? "HighRisk" : warnings.Count > 0 ? "NeedsAttention" : "ReadyForMerge";
         var summary = $"Review intelligence: {state}, risk {score:0.##}/100, {blockers.Count} blocker(s), {warnings.Count} warning(s).";
         var result = new ReviewIntelligenceResult(state, score, blockers, warnings, required.OrderBy(x => x).ToArray(), actions.Distinct().ToArray(), summary);
        var prKey = c.PullRequestNumber.HasValue ? $"pr:{c.RepositoryKey}:{c.PullRequestNumber.Value}" : $"commit:{c.RepositoryKey}:{c.Revision}";
         var graphNodes = await _graph.ListNodesAsync(c.ProjectId, null, ct);
         var target = graphNodes.FirstOrDefault(x => x.Key.Equals(prKey, StringComparison.OrdinalIgnoreCase));
        if (target is not null) { var node = await _graphService.UpsertNodeAsync(new(c.ProjectId, DevelopmentGraphNodeType.Review, $"review-assessment:{c.SubjectKey}", c.SubjectKey, state, "DIP", target.ExternalReference, JsonSerializer.Serialize(result)), ct);
         await _graphService.LinkAsync(new(c.ProjectId, node.Id, target.Id, DevelopmentGraphEdgeType.Reviews, Math.Max(50m, score), blockers.Count > 0, "{}"), ct);
         }
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(), c.ProjectId, c.SubjectKey, P2IntelligenceKind.ReviewIntelligence, blockers.Count > 0 ? P2IntelligenceStatus.Warning : P2IntelligenceStatus.Informational, score, EvidenceConfidence.High, summary, JsonSerializer.Serialize(result), target?.ExternalReference, _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
         return result;
    }
    private static bool IsSource(string p) => new[] { ".cs", ".cpp", ".cc", ".cxx", ".c", ".h", ".hpp", ".py" }.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase);
    private static bool IsAsset(string p) => new[] { ".fbx", ".obj", ".png", ".jpg", ".tga", ".psd", ".wav", ".mp3", ".uasset", ".umap", ".prefab", ".unity", ".mat", ".anim" }.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase);
}
