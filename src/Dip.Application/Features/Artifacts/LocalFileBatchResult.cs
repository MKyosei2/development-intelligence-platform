// Purpose: Defines the LocalFileBatchResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Artifacts;

public sealed record LocalFileBatchResult(int Received, int Accepted, int Duplicate, int Rejected, IReadOnlyList<FileObservationResult> Results, IReadOnlyList<string> Warnings);
