// Purpose: Defines the request used to update project settings.
namespace Dip.Application.Features.Projects;

public sealed record UpdateProjectCommand(
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    DateTimeOffset? ReleaseDateUtc,
    string Reason = "Project settings updated from the DIP UI.");
