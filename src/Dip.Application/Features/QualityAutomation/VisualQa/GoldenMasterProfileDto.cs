// Purpose: Defines the GoldenMasterProfileDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record GoldenMasterProfileDto(Guid Id, Guid ProjectId, string CaptureKey, Guid ArtifactRecordId, string SceneKey, string CameraKey, int Width, int Height, IReadOnlyList<VisualMaskRegion> Masks, bool Active, string ApprovedBy, DateTimeOffset ApprovedAtUtc);
