// Purpose: Defines the VisualComparisonDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record VisualComparisonDto(Guid Id,Guid ProjectId,Guid GoldenMasterProfileId,Guid BaselineArtifactRecordId,Guid CurrentArtifactRecordId,Guid? DiffArtifactRecordId,VisualComparisonVerdict Verdict,decimal ChangedPixelPercent,decimal Ssim,int ChangedPixelCount,int EvaluatedPixelCount,string Summary,DateTimeOffset ComparedAtUtc);
