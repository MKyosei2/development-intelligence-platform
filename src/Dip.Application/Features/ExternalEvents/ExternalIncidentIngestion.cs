// Purpose: Implements External Incident Ingestion responsibilities for external events.
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.ExternalEvents;

public sealed record ExternalIncidentContext(Guid ProjectId, string SubjectKey, string Provider, string? CommitSha, string? BuildId, LanguageId Language);
public sealed record ExternalIncidentEnvelope(string EventId, string Title, string Message, string? ExceptionType, string? StackTrace, string? FilePath, int? Line, string? SceneOrRoute, IReadOnlyDictionary<string, string> Tags, DateTimeOffset ObservedAtUtc);
public interface IExternalIncidentIngestionAdapter
{
    string Provider { get; }
    bool CanHandle(string provider);
    ExternalIncidentEnvelope Parse(JsonElement payload);
}

public sealed class ExternalIncidentIngestionService(IEnumerable<IExternalIncidentIngestionAdapter> adapters, IRuntimeDefectIntelligence runtime)
{
    private readonly IReadOnlyList<IExternalIncidentIngestionAdapter> _adapters = adapters.ToArray();
    public async Task<RuntimeDefectResult> IngestAsync(ExternalIncidentContext context, JsonElement payload, CancellationToken ct)
    {
        var adapter = _adapters.FirstOrDefault(x => x.CanHandle(context.Provider)) ?? throw new NotSupportedException($"External incident provider '{context.Provider}' is not configured.");
        var e = adapter.Parse(payload); return await runtime.ObserveAsync(new(context.ProjectId, context.SubjectKey, context.Language, context.Provider, e.Title, e.Message, e.ExceptionType, e.StackTrace, context.CommitSha, context.BuildId, e.SceneOrRoute, e.FilePath, e.Line, e.Tags, e.ObservedAtUtc), ct);
    }
    public IReadOnlyList<string> Providers => _adapters.Select(x => x.Provider).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
}
