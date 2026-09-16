// Purpose: Defines the UpdateQualityAutomationProfileCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record UpdateQualityAutomationProfileCommand(bool Enabled,decimal ChangedPixelWarningPercent,decimal ChangedPixelFailurePercent,decimal MinimumSsim,string AssetRulesJson);
