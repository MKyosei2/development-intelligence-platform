// Purpose: Defines the RunVisualComparisonCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record RunVisualComparisonCommand(string CaptureKey,Guid CurrentArtifactRecordId);
