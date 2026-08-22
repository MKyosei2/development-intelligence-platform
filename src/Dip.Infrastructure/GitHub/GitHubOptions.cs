namespace Dip.Infrastructure.GitHub;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string WebhookSecret { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string ProductHeader { get; init; } = "development-intelligence-platform";
    public string QualityReportMarker { get; init; } = "<!-- dip-quality-report -->";
    public int MaxRetryAttempts { get; init; } = 4;

    // GitHub App repository connection. The private key is server-side only.
    public long AppId { get; init; }
    public long InstallationId { get; init; }
    public string AppSlug { get; init; } = string.Empty;
    public string AppPrivateKeyPath { get; init; } = string.Empty;
    public string AppPrivateKeyBase64 { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = "https://api.github.com/";
    public int MaxRepositoryPages { get; init; } = 10;
    public int MaxPullRequestPages { get; init; } = 20;
    public int MaxPullRequestCommitPages { get; init; } = 3;
    public int MaxPullRequestFilePages { get; init; } = 30;
    public int MaxPullRequestReviewPages { get; init; } = 20;
    public int WorkspaceMaxArchiveMb { get; init; } = 512;

    public bool HasGitHubAppCredentials =>
        AppId > 0
        && InstallationId > 0
        && (!string.IsNullOrWhiteSpace(AppPrivateKeyPath)
            || !string.IsNullOrWhiteSpace(AppPrivateKeyBase64));
}
