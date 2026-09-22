// Purpose: Defines VisualComparisonComputation.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record VisualComparisonComputation(bool Compatible, int Width, int Height, int ChangedPixelCount, int EvaluatedPixelCount, decimal ChangedPixelPercent, decimal Ssim, byte[]? DiffPng, string? IncompatibilityReason);
