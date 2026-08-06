using System.Threading.RateLimiting;
using Dip.Api.Middleware;
using Dip.Application;
using Dip.Infrastructure;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddGitHubApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGitHubIntegration(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        "github-webhook",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection.RemoteIpAddress
                        ?.ToString()
                    ?? "unknown",
                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
});

var app = builder.Build();

// The repository currently contains no EF Core migration classes.
// InitializeDatabaseAsync applies migrations when they exist and otherwise
// creates the schema from the current model.
await app.Services.InitializeDatabaseAsync();

app.UseForwardedHeaders();

// Exception handling must wrap custom request-body middleware.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseGitHubWebhookBodyBuffering();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
