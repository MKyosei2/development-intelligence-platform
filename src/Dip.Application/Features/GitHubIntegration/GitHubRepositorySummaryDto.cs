// Purpose: Defines the GitHubRepositorySummaryDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubRepositorySummaryDto(
    long Id,
    string Owner,
    string Name,
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    bool Private,
    bool Archived,
    string? PrimaryLanguage,
    string? Description,
    int OpenIssuesCount,
    int StarsCount,
    int ForksCount,
    DateTimeOffset? UpdatedAtUtc,
    Guid? DipProjectId = null);
