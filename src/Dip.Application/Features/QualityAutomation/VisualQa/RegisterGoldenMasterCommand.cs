// Purpose: Defines the RegisterGoldenMasterCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record RegisterGoldenMasterCommand(string CaptureKey,Guid ArtifactRecordId,string SceneKey,string CameraKey,IReadOnlyList<VisualMaskRegion> Masks,string ApprovedBy);
