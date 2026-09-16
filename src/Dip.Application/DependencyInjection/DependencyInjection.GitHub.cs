// Purpose: Registers D ep en de nc yI nj ec ti on.G it Hu b application dependencies.
using Dip.Application.Features.GitHub;using Microsoft.Extensions.DependencyInjection;
namespace Dip.Application;
public static class GitHubApplicationDependencyInjection{public static IServiceCollection AddGitHubApplication(this IServiceCollection services){services.AddScoped<GitHubWebhookProcessor>();services.AddScoped<GitHubDevelopmentGraphIngestor>();return services;}}
