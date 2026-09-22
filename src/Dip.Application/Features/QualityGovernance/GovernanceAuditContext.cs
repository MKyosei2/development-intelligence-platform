// Purpose: Defines the GovernanceAuditContext application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record GovernanceAuditContext(string Actor, string? RequestId = null, string? CorrelationId = null);
