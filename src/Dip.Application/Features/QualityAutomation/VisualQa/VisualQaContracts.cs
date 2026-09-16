// Purpose: Coordinates V is ua lQ aC on tr ac ts application behavior for QualityAutomation.
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record VisualMaskRegion(int X, int Y, int Width, int Height);

public sealed record VisualComparisonComputation(
    bool Compatible,
    int Width,
    int Height,
    int ChangedPixelCount,
    int EvaluatedPixelCount,
    decimal ChangedPixelPercent,
    decimal Ssim,
    byte[]? DiffPng,
    string? IncompatibilityReason);

public sealed record QualityAutomationProfileDto(
    Guid Id,
    Guid ProjectId,
    bool Enabled,
    decimal ChangedPixelWarningPercent,
    decimal ChangedPixelFailurePercent,
    decimal MinimumSsim,
    string AssetRulesJson,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdateQualityAutomationProfileCommand(
    bool Enabled,
    decimal ChangedPixelWarningPercent,
    decimal ChangedPixelFailurePercent,
    decimal MinimumSsim,
    string AssetRulesJson);

public sealed record RegisterGoldenMasterCommand(
    string CaptureKey,
    Guid ArtifactRecordId,
    string SceneKey,
    string CameraKey,
    IReadOnlyList<VisualMaskRegion> Masks,
    string ApprovedBy);

public sealed record GoldenMasterProfileDto(
    Guid Id,
    Guid ProjectId,
    string CaptureKey,
    Guid ArtifactRecordId,
    string SceneKey,
    string CameraKey,
    int Width,
    int Height,
    IReadOnlyList<VisualMaskRegion> Masks,
    bool Active,
    string ApprovedBy,
    DateTimeOffset ApprovedAtUtc);

public sealed record RunVisualComparisonCommand(
    string CaptureKey,
    Guid CurrentArtifactRecordId);

public sealed record VisualComparisonDto(
    Guid Id,
    Guid ProjectId,
    Guid GoldenMasterProfileId,
    Guid BaselineArtifactRecordId,
    Guid CurrentArtifactRecordId,
    Guid? DiffArtifactRecordId,
    VisualComparisonVerdict Verdict,
    decimal ChangedPixelPercent,
    decimal Ssim,
    int ChangedPixelCount,
    int EvaluatedPixelCount,
    string Summary,
    DateTimeOffset ComparedAtUtc);
