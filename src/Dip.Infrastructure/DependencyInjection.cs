using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Infrastructure.Messaging;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Dip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnection = configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(postgresConnection))
            throw new InvalidOperationException("ConnectionStrings:PostgreSql is missing.");

        services.AddDbContext<DipDbContext>(options => options.UseNpgsql(postgresConnection));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IPipelineRunRepository, PipelineRunRepository>();
        services.AddScoped<IMetricRepository, MetricRepository>();
        services.AddScoped<IQualityIssueRepository, QualityIssueRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DipDbContext>());
        services.AddSingleton<ISystemClock, SystemClock>();

        services.Configure<RedisQueueOptions>(configuration.GetSection(RedisQueueOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConnection = configuration["Redis:ConnectionString"]
                ?? configuration.GetConnectionString("Redis")
                ?? "localhost:6379";
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = Math.Max(options.ConnectRetry, 3);
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IQueueClient, RedisQueueClient>();
        return services;
    }
}
