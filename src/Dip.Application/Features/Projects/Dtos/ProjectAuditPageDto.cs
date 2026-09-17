// Purpose: Defines one cursor-paginated page of project audit events.
namespace Dip.Application.Features.Projects;

public sealed record ProjectAuditPageDto(
    IReadOnlyList<ProjectAuditEntryDto> Items,
    string? NextCursor,
    bool HasMore);
