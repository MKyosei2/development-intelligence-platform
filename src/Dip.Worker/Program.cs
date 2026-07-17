using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Quality;
using Dip.Infrastructure.Messaging;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Worker.Workers;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RedisQueueOptions>(
    builder.Configuration.GetSection(RedisQueueOptions.SectionName));

builder.Services.AddDbContext<DipDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSql")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException(
            "Redis connection string is missing.");

    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddScoped<IUnitOfWork>(sp =>
    sp.GetRequiredService<DipDbContext>());
builder.Services.AddScoped<IPipelineRunRepository, PipelineRunRepository>();
builder.Services.AddScoped<IMetricRepository, MetricRepository>();
builder.Services.AddScoped<IQualityIssueRepository, QualityIssueRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<DashboardReadRepository>();
builder.Services.AddScoped<QualityGateEvaluator>();

builder.Services.AddSingleton<IQueueClient, RedisQueueClient>();

builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<CollectorReportProcessedWorker>();

var host = builder.Build();
await host.RunAsync();
