// Purpose: Defines the FileSnapshot application contract.
namespace Dip.Agent.Core;

public sealed record FileSnapshot(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    string Sha256);
