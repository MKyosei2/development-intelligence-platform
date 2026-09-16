// Purpose: Defines the GitHubCapabilityProbeDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubCapabilityProbeDto(
    bool Metadata,
    bool Contents,
    bool PullRequests,
    bool Actions);
