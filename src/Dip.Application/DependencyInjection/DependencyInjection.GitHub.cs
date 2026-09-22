// Purpose: Defines a focused responsibility within the development intelligence platform.
using Dip.Application.Features.GitHub;
using Microsoft.Extensions.DependencyInjection;
namespace Dip.Application;
public static class GitHubApplicationDependencyInjection
{
    public static IServiceCollection AddGitHubApplication(this IServiceCollection services)
    {
        services.AddScoped<GitHubWebhookProcessor>();
        services.AddScoped<GitHubDevelopmentGraphIngestor>();
        return services;
    }
}
