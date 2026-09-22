// Purpose: Defines the ManagementSummaryDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementSummaryDto(string OverallStatus, decimal HealthScore, string PrimaryConcern, IReadOnlyList<string> KeyDrivers, IReadOnlyList<string> DecisionsRequired, IReadOnlyList<string> RecommendedActions, string PlainLanguageSummary);
