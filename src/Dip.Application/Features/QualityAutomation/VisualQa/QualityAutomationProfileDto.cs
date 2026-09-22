// Purpose: Defines the QualityAutomationProfileDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record QualityAutomationProfileDto(Guid Id, Guid ProjectId, bool Enabled, decimal ChangedPixelWarningPercent, decimal ChangedPixelFailurePercent, decimal MinimumSsim, string AssetRulesJson, DateTimeOffset UpdatedAtUtc);
