// Purpose: Defines the BugMinimizationDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record BugMinimizationDto(Guid Id, string Status, int OriginalUnits, int MinimizedUnits, IReadOnlyList<string> MinimizedArguments, string Detail);
