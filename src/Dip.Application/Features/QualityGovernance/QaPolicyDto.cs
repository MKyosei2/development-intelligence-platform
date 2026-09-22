// Purpose: Defines the QaPolicyDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaPolicyDto(Guid Id, Guid ProjectId, string Name, string Version, string RulesJson, QaPolicyLifecycleStatus LifecycleStatus, int Revision, Guid? SupersedesPolicyId, string? ApprovedBy, string? ActivatedBy, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? ApprovedAtUtc, DateTimeOffset? ActivatedAtUtc, long ConcurrencyVersion);
