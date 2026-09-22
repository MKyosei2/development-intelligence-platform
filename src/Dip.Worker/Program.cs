// Purpose: Composes and starts the DIP background worker host.
using Dip.Application;
using Dip.Application.Features.Jobs;
using Dip.Infrastructure;
using Dip.Worker.Options;
using Dip.Worker.Workers;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.Configure<DurableJobQueueOptions>(builder.Configuration.GetSection(DurableJobQueueOptions.SectionName));
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("dip-worker"))
    .WithTracing(t => t.AddHttpClientInstrumentation().AddSource("DIP.DevelopmentIntelligence").AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(m => m.AddHttpClientInstrumentation().AddRuntimeInstrumentation().AddMeter("DIP.DevelopmentIntelligence").AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));
builder.Services.AddScoped<IDurableJobHandler, FuzzJobHandler>();
builder.Services.AddScoped<IDurableJobHandler, MutationJobHandler>();
builder.Services.AddScoped<IDurableJobHandler, SoakJobHandler>();
builder.Services.AddScoped<IDurableJobHandler, GitBisectJobHandler>();
builder.Services.AddScoped<IDurableJobHandler, MediaRegressionJobHandler>();
builder.Services.AddScoped<IDurableJobHandler, BenchmarkJobHandler>();
builder.Services.AddScoped<IDurableJobHandler, GameSessionReplayJobHandler>();
builder.Services.AddScoped<IDurableJobHandler, ReleaseEvidenceJobHandler>();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<CollectorReportProcessedWorker>();
builder.Services.AddHostedService<DurableJobWorker>();
builder.Services.AddHostedService<DurableJobRecoveryWorker>();
builder.Services.AddHostedService<ArtifactRetentionWorker>();
builder.Services.AddHostedService<WorkerMetricsEndpoint>();
builder.Services.AddHostedService<AutomaticRegressionTriageWorker>();
builder.Services.AddHostedService<GitHubChangeRiskWorker>();
builder.Services.AddHostedService<ManagementNotificationWorker>();
var host = builder.Build(); await host.RunAsync();
