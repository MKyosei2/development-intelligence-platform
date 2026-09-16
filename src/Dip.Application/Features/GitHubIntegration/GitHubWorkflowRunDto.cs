// Purpose: Defines the GitHubWorkflowRunDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubWorkflowRunDto(
    long Id,
    string Name,
    string Event,
    string Status,
    string? Conclusion,
    string HeadBranch,
    string HeadSha,
    string HtmlUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
