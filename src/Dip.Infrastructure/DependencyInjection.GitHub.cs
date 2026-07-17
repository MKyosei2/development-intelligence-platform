using Dip.Application.Abstractions.GitHub;
using Dip.Application.Abstractions.Persistence;
using Dip.Infrastructure.GitHub;
using Dip.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Octokit;

namespace Dip.Infrastructure;

public static class GitHubDependencyInjection
{
    public static IServiceCollection AddGitHubIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(
            configuration.GetSection(GitHubOptions.SectionName));

        services.AddSingleton<GitHubClient>(GitHubClientFactory.Create);
        services.AddSingleton<
            IGitHubWebhookVerifier,
            GitHubWebhookVerifier>();
        services.AddScoped<
            IGitHubPullRequestCommentService,
            GitHubPullRequestCommentService>();
        services.AddScoped<
            IProcessedWebhookRepository,
            ProcessedWebhookRepository>();

        return services;
    }
}
