// Purpose: Registers evidence, integrity verification, integration, credential, policy, and artifact-tracking infrastructure boundaries.
using System.Net.Http.Headers;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Security;
using Dip.Application.Features.Evidence;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Policies;
using Dip.Infrastructure.Integrations.GitHub;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure;

public static class EvidenceIntegrationDependencyInjection
{
    public static IServiceCollection AddEvidenceIntegrationFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IEvidenceV2Repository, EvidenceV2Repository>();
        services.AddScoped<IIntegrationConnectionRepository, IntegrationConnectionRepository>();
        services.AddScoped<IProjectSourceBindingRepository, ProjectSourceBindingRepository>();
        services.AddScoped<IPolicyProfileRepository, PolicyProfileRepository>();

        services
            .AddOptions<SecretsBrokerOptions>()
            .Bind(configuration.GetSection(SecretsBrokerOptions.SectionName))
            .PostConfigure(options =>
            {
                options.Provider = FirstNonEmpty(
                    Environment.GetEnvironmentVariable("DIP_SECRET_PROVIDER"),
                    options.Provider,
                    "local") ?? "local";
                options.BrokerUrl = FirstNonEmpty(
                    Environment.GetEnvironmentVariable("DIP_SECRET_BROKER_URL"),
                    options.BrokerUrl);
                options.BrokerToken = FirstNonEmpty(
                    Environment.GetEnvironmentVariable("DIP_SECRET_BROKER_TOKEN"),
                    options.BrokerToken);
            })
            .Validate(
                options => options.IsValid(out _),
                "Invalid DIP secrets broker configuration. Check Provider and BrokerUrl.")
            .ValidateOnStart();

        services.AddSingleton<LocalIntegrationCredentialStore>();
        services.AddHttpClient<EnterpriseIntegrationCredentialStore>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<SecretsBrokerOptions>>().Value;
            if (!options.UseBroker)
                return;

            client.BaseAddress = EnsureTrailingSlash(options.BrokerUrl!);
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrWhiteSpace(options.BrokerToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.BrokerToken);
            }
        });
        services.AddTransient<IIntegrationCredentialStore>(provider =>
            provider.GetRequiredService<EnterpriseIntegrationCredentialStore>());

        services.AddScoped<IIntegrationConnector, GitHubConnector>();
        services.AddScoped<IntegrationConnectorRegistry>();

        services.AddScoped<PolicyResolver>();
        services.AddScoped<PolicyProfileService>();
        services.AddScoped<EvidenceIntegrityVerifier>();
        services.AddScoped<EvidenceIngestionService>();
        services.AddScoped<IntegrationConnectionService>();

        services.AddArtifactTrackingFoundation(configuration);
        return services;
    }

    private static Uri EnsureTrailingSlash(string value)
    {
        var normalized = value.EndsWith('/') ? value : value + "/";
        return new Uri(normalized, UriKind.Absolute);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
