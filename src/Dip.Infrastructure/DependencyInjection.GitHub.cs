using Dip.Application.Abstractions.GitHub;
using Dip.Application.Abstractions.Persistence;
using Dip.Infrastructure.GitHub;
using Dip.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octokit;

namespace Dip.Infrastructure;

public static class GitHubDependencyInjection
{
    public static IServiceCollection AddGitHubIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<GitHubOptions>(
            configuration.GetSection(GitHubOptions.SectionName));

        services.AddSingleton<GitHubClient>(serviceProvider =>
            GitHubClientFactory.Create(
                serviceProvider.GetRequiredService<
                    IOptions<GitHubOptions>>()));

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
