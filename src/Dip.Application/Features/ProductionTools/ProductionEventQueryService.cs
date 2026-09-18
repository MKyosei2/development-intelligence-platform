// Purpose: Orchestrates the DIP workflow for P ro du ct io nE ve nt Qu er yS er vi ce.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProductionTools;

public sealed class ProductionEventQueryService
{
    private readonly IEvidenceRepository _evidence;

    public ProductionEventQueryService(IEvidenceRepository evidence) => _evidence = evidence;

    public async Task<IReadOnlyList<ProductionEventDto>> ListAsync(Guid projectId, int take, CancellationToken ct)
    {
        var rows = await _evidence.ListForProjectAsync(projectId, null, ct);
        return rows
            .Where(x => x.SourceType is EvidenceSourceType.Maya or EvidenceSourceType.Unity or EvidenceSourceType.Unreal)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => new ProductionEventDto(
                x.Id,
                x.SourceType switch
                {
                    EvidenceSourceType.Maya => ProductionToolSource.Maya,
                    EvidenceSourceType.Unity => ProductionToolSource.Unity,
                    EvidenceSourceType.Unreal => ProductionToolSource.Unreal,
                    _ => ProductionToolSource.Unknown
                },
                ParseKind(x.MetadataValue("eventKind")),
                x.Title,
                x.Summary,
                x.ActorId,
                x.MachineId,
                x.ArtifactId,
                x.ContentHash,
                x.ObservedAtUtc,
                x.MetadataJson))
            .ToArray();
    }

    private static ProductionToolEventKind ParseKind(string? value)
        => Enum.TryParse<ProductionToolEventKind>(value, true, out var parsed)
            ? parsed
            : ProductionToolEventKind.Unknown;
}
