// Purpose: Defines the request used to create a project.
namespace Dip.Application.Features.Projects;

public sealed record CreateProjectCommand(
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    DateTimeOffset? ReleaseDateUtc);
