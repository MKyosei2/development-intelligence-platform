// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Abstractions.Persistence;
using Dip.Infrastructure.GitHub;
using Dip.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IGitHubWebhookVerifier, GitHubWebhookVerifier>();
        services.AddScoped<IProcessedWebhookRepository, ProcessedWebhookRepository>();
        services.AddSingleton<IGitHubRepositoryCatalog, GitHubRepositoryCatalog>();
        services.AddSingleton<Dip.Application.Abstractions.Integrations.IVersionControlConnector,
            Dip.Infrastructure.Integrations.GitHubVersionControlConnector>();

        // Difficulty-1 foundation is registered here so existing API Program.cs
        // remains unchanged and the legacy GitHub integration keeps working.
        services.AddEvidenceIntegrationFoundation(configuration);
        return services;
    }
}
