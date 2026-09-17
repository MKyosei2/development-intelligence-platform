// Purpose: Defines the project data returned by project-management operations.
namespace Dip.Application.Features.Projects;

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    decimal HealthScore,
    DateTimeOffset? ReleaseDateUtc,
    string LifecycleState = "Active",
    DateTimeOffset? LifecycleChangedAtUtc = null);
