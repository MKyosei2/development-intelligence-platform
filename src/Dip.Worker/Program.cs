using Dip.Application;
using Dip.Infrastructure;
using Dip.Worker.Options;
using Dip.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<CollectorReportProcessedWorker>();
var host = builder.Build();
await host.RunAsync();
