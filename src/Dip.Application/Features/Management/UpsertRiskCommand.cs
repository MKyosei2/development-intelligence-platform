// Purpose: Defines the UpsertRiskCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertRiskCommand(Guid ProjectId, string Key, string Title, string Description, decimal ProbabilityPercent, decimal ImpactPercent, string Status = "Open", string Category = "Delivery", string? OwnerRole = null, DateTimeOffset? ReviewDueUtc = null, string? Trigger = null, string? Mitigation = null, IReadOnlyList<ManagementReference>? AffectedItems = null, string? ExternalReference = null);
