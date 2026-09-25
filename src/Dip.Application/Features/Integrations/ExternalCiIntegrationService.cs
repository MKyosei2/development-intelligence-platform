// Purpose: Coordinates External CI Integration application responsibilities for integrations.
using System.Text.Json;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Integrations;

public sealed class ExternalCiIntegrationService
{
    private readonly IReadOnlyDictionary<ExternalCiProvider, IExternalCiConnector> _connectors;
     private readonly DevelopmentGraphService _graph;
     private readonly IP2IntelligenceRepository _records;
     private readonly IUnitOfWork _uow;
     private readonly ISystemClock _clock;
    public ExternalCiIntegrationService(IEnumerable<IExternalCiConnector> connectors, DevelopmentGraphService graph, IP2IntelligenceRepository records, IUnitOfWork uow, ISystemClock clock) { _connectors = connectors.GroupBy(x => x.Provider).ToDictionary(x => x.Key, x => x.First());
     _graph = graph;
     _records = records;
     _uow = uow;
     _clock = clock;
     }
    public async Task<NormalizedCiRun> SyncAsync(ExternalCiSyncRequest request, CancellationToken ct) { if (!_connectors.TryGetValue(request.Provider, out var connector)) throw new InvalidOperationException($"No {request.Provider} CI connector is registered.");
     var result = await connector.FetchAsync(request, ct);
     return await IngestAsync(result, ct);
     }
    public async Task<NormalizedCiRun> IngestAsync(NormalizedCiRun run, CancellationToken ct) { await _graph.IngestCiRunAsync(run, ct);
     var score = run.FailedTests > 0 ? Math.Min(100m, 20m + run.FailedTests * 5m) : 0m;
     var summary = $"{run.Provider} pipeline '{run.PipelineKey}' run {run.RunId}: {run.Status}; tests {run.PassedTests} passed / {run.FailedTests} failed / {run.SkippedTests} skipped.";
     await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(), run.ProjectId, $"ci:{run.Provider}:{run.PipelineKey}:{run.RunId}", P2IntelligenceKind.ExternalCi, run.FailedTests > 0 ? P2IntelligenceStatus.Warning : P2IntelligenceStatus.Informational, score, EvidenceConfidence.High, summary, JsonSerializer.Serialize(run), run.Url, _clock.UtcNow), ct);
     await _uow.SaveChangesAsync(ct);
     return run;
     }
}
