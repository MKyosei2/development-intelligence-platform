// Purpose: Defines the GitHubConnectionStatusDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubConnectionStatusDto(
    bool Configured,
    bool Connected,
    string Mode,
    long? InstallationId,
    string? Account,
    string? AppSlug,
    string? InstallUrl,
    string Message);
