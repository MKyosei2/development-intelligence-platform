// Purpose: Defines the StoredProductionMediaDto application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProductionTools;

public sealed record StoredProductionMediaDto(Guid ArtifactRecordId, Guid? ArtifactPreviewId, Guid? TrackedArtifactId, Guid? TrackedArtifactVersionId, string Sha256, long SizeBytes, string ContentType, ProductionMediaKind Kind, string RelativePath, Guid? VisualComparisonId = null, string? VisualComparisonVerdict = null);
