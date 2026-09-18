// Purpose: Provides the infrastructure capability represented by G it Hu bC li en tF ac to ry.
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
