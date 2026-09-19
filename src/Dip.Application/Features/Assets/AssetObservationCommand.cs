// Purpose: Defines an observed asset and its import/reference/platform quality signals.
namespace Dip.Application.Features.Assets;

public sealed record AssetObservationCommand(
    Guid ProjectId,
    string RepositoryKey,
    string Path,
    string AssetType,
    long SizeBytes,
    string? ContentHash,
    IReadOnlyList<string>? Dependencies,
    IReadOnlyList<string>? Issues,
    string? CommitSha,
    string? Platform,
    bool ImportSucceeded = true,
    bool MissingReference = false,
    bool PlatformIncompatible = false);
