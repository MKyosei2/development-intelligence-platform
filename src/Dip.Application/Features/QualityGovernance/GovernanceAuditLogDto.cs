// Purpose: Defines the GovernanceAuditLogDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record GovernanceAuditLogDto(Guid Id, Guid ProjectId, string Category, string Action, string Actor, string? SubjectKey, string? DetailJson, string? RequestId, string? CorrelationId, string? Reason, DateTimeOffset CreatedAtUtc);
