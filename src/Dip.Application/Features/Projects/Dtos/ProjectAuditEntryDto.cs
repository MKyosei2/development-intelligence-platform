// Purpose: Defines one reviewer-facing project audit event.
namespace Dip.Application.Features.Projects;

public sealed record ProjectAuditEntryDto(
    Guid Id,
    Guid ProjectId,
    string Category,
    string Action,
    string Actor,
    string? SubjectKey,
    string? DetailJson,
    string? RequestId,
    string? CorrelationId,
    string? Reason,
    DateTimeOffset CreatedAtUtc);
