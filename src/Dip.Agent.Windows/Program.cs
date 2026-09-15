using Dip.Agent.Core;
using Dip.Agent.Windows;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "DIP Project Agent");
builder.Services.AddSingleton(provider => AgentConfiguration.Load(builder.Configuration));
builder.Services.AddSingleton<AgentRuntime>();
builder.Services.AddHostedService<CollectorWorker>();
builder.Services.AddHostedService<ToolBridgeWorker>();
builder.Services.AddHostedService<FleetWorker>();
await builder.Build().RunAsync();
