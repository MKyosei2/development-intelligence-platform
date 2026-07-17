using Dip.Application;
using Dip.Infrastructure;
using Dip.Infrastructure.Messaging;
using Dip.Worker.Options;
using Dip.Worker.Workers;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<RedisQueueOptions>(
    builder.Configuration.GetSection(RedisQueueOptions.SectionName));
builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RedisQueueOptions>>().Value;
    return ConnectionMultiplexer.Connect(options.ConnectionString);
});

builder.Services.AddSingleton<
    Dip.Application.Abstractions.Messaging.IQueueClient,
    RedisQueueClient>();

builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<AnalysisRequestWorker>();
builder.Services.AddHostedService<NotificationWorker>();

await builder.Build().RunAsync();
