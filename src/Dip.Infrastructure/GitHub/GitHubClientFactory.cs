// Purpose: Implements G it Hu bC li en tF ac to ry infrastructure behavior for g it hu b.
using Microsoft.Extensions.Options;
using Octokit;

namespace Dip.Infrastructure.GitHub;

public static class GitHubClientFactory
{
    public static GitHubClient Create(
        IOptions<GitHubOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Token))
            throw new InvalidOperationException("GitHub:Token is missing.");

        return new GitHubClient(
            new ProductHeaderValue(settings.ProductHeader))
        {
            Credentials = new Credentials(settings.Token)
        };
    }
}
