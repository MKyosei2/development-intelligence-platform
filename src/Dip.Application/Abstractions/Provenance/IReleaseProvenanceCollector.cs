// Purpose: Implements I Release Provenance Collector responsibilities for provenance.
namespace Dip.Application.Abstractions.Provenance;
public sealed record ProvenanceComponent(string Type, string Name, string Version, string? Path, string? Sha256);
public sealed record ProvenanceSnapshot(string RepositoryRoot, string Runtime, string OperatingSystem, IReadOnlyList<ProvenanceComponent> Components, IReadOnlyDictionary<string, string> Files);
public interface IReleaseProvenanceCollector
{
    Task<ProvenanceSnapshot> CollectAsync(string repositoryRoot, CancellationToken ct);
}
