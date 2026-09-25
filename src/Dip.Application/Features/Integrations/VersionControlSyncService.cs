// Purpose: Implements Version Control Sync Service application behavior for integrations.
using System.Text.Json;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ChangeImpact;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Review;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Integrations;

public sealed class VersionControlSyncService
{
    private readonly IReadOnlyDictionary<VersionControlProvider, IVersionControlConnector> _connectors;
     private readonly DevelopmentGraphService _graph;
     private readonly ChangeImpactAnalysisService _impact;
     private readonly ReviewIntelligenceService _reviews;
     private readonly IP2IntelligenceRepository _records;
     private readonly IUnitOfWork _uow;
     private readonly ISystemClock _clock;
    public VersionControlSyncService(IEnumerable<IVersionControlConnector> connectors, DevelopmentGraphService graph, ChangeImpactAnalysisService impact, ReviewIntelligenceService reviews, IP2IntelligenceRepository records, IUnitOfWork uow, ISystemClock clock) { _connectors = connectors.GroupBy(x => x.Provider).ToDictionary(x => x.Key, x => x.First());
     _graph = graph;
     _impact = impact;
     _reviews = reviews;
     _records = records;
     _uow = uow;
     _clock = clock;
     }
    public async Task<NormalizedVersionControlChange> SyncAsync(VersionControlSyncRequest request, CancellationToken ct)
    {
        if (!_connectors.TryGetValue(request.Provider, out var connector)) throw new InvalidOperationException($"No {request.Provider} version-control connector is registered.");
        var result = await connector.FetchAsync(request, ct); await _graph.IngestVersionControlChangeAsync(result, ct);
        var changedKeys = new List<string> { $"commit:{request.RepositoryKey}:{result.Revision}" }; changedKeys.AddRange(result.Files.Select(f => $"file:{request.RepositoryKey}:{f.Path.Replace('\\', '/').Trim('/')}"));
        await _impact.AnalyzeAsync(new(request.ProjectId, $"vcs:{request.RepositoryKey}:{result.Revision}", changedKeys), ct);
        if (result.PullRequestNumber.HasValue)
        {
            var approvals = (result.Reviews ?? Array.Empty<NormalizedReview>()).Count(r => r.State.Equals("Approved", StringComparison.OrdinalIgnoreCase) || r.State.Equals("Approve", StringComparison.OrdinalIgnoreCase));
            var changesRequested = (result.Reviews ?? Array.Empty<NormalizedReview>()).Count(r => r.State.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase).Equals("ChangesRequested", StringComparison.OrdinalIgnoreCase));
            var paths = result.Files.Select(x => x.Path).ToArray();
             var testsChanged = paths.Any(IsTestPath);
             var publicApiChanged = paths.Any(IsPublicApiPath);
             var schemaChanged = paths.Any(IsSchemaPath);
            await _reviews.AssessAsync(new(request.ProjectId, $"pr:{request.RepositoryKey}:{result.PullRequestNumber.Value}", request.RepositoryKey, result.Revision, result.PullRequestNumber, paths, approvals, changesRequested, 0, 1, testsChanged, null, publicApiChanged, schemaChanged), ct);
        }
        var prSummary = result.PullRequestNumber.HasValue ? $", PR number {result.PullRequestNumber.Value}" : string.Empty;
         var summary = $"{request.Provider} change {result.Revision} synchronized: {result.Files.Count} changed file(s){prSummary}.";
         await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(), request.ProjectId, $"vcs:{request.RepositoryKey}:{result.Revision}", P2IntelligenceKind.VersionControlSync, P2IntelligenceStatus.Informational, null, EvidenceConfidence.High, summary, JsonSerializer.Serialize(result), result.Url, _clock.UtcNow), ct);
         await _uow.SaveChangesAsync(ct);
         return result;
    }
    private static bool IsTestPath(string path) { var p = path.Replace('\\', '/'); return p.Contains("/test", StringComparison.OrdinalIgnoreCase) || p.Contains("/tests/", StringComparison.OrdinalIgnoreCase) || p.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) || p.EndsWith("_test.py", StringComparison.OrdinalIgnoreCase) || p.EndsWith("_test.cpp", StringComparison.OrdinalIgnoreCase); }
    private static bool IsPublicApiPath(string path) { var p = path.Replace('\\', '/'); return p.Contains("/Contracts/", StringComparison.OrdinalIgnoreCase) || p.Contains("/Public/", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".h", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase); }
    private static bool IsSchemaPath(string path) { var p = path.Replace('\\', '/'); return p.Contains("Migration", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) || p.EndsWith("schema.json", StringComparison.OrdinalIgnoreCase); }
}
