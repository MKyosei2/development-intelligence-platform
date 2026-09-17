// Purpose: Defines the reason attached to a project lifecycle transition.
namespace Dip.Application.Features.Projects;

public sealed record ProjectLifecycleCommand(
    string Reason = "Project lifecycle changed from the DIP UI.");
