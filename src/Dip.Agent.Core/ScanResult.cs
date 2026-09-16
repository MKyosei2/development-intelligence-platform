// Purpose: Defines the ScanResult application contract.
namespace Dip.Agent.Core;

public sealed record ScanResult(
    IReadOnlyList<LocalFileObservation> Observations,
    IReadOnlyDictionary<string, FileSnapshot> Snapshot,
    IReadOnlyList<string> Warnings);
