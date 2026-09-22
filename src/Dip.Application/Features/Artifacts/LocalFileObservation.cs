// Purpose: Defines the LocalFileObservation application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Artifacts;

public sealed record LocalFileObservation(string EventId, FileObservationKind Kind, string RelativePath, string? PreviousRelativePath, string? Extension, long? SizeBytes, string? Sha256, DateTimeOffset? FileModifiedAtUtc, DateTimeOffset ObservedAtUtc);
