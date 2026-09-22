// Purpose: Composes and starts the DIP API host.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Dip.Api.Authorization;
using Dip.Api.Controllers;
using Dip.Api.Hubs;
using Dip.Api.Middleware;
using Dip.Application;
using Dip.Infrastructure;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddGitHubApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGitHubIntegration(builder.Configuration);

builder.Services.AddScoped<ProjectScopeAuthorizationFilter>();

builder.Services
    .AddControllers(options =>
    {
        options.SuppressAsyncSuffixInActionNames = false;
        options.Filters.AddService<ProjectScopeAuthorizationFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ProjectRealtimePublisher>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ApiMetricsRegistry>();
builder.Services.AddHttpClient<GitHubOAuthClient>();
builder.Services.AddHttpClient("system-status", client => client.Timeout = TimeSpan.FromSeconds(5));

var otlpEndpoint =
    builder.Configuration["OpenTelemetry:OtlpEndpoint"]
    ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("dip-api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("DIP.DevelopmentIntelligence")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("DIP.DevelopmentIntelligence")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

var authenticationSection =
    builder.Configuration.GetSection(AuthOptions.SectionName);
var authenticationOptions =
    authenticationSection.Get<AuthOptions>() ?? new AuthOptions();

ProductionSecurityGuard.Validate(
    builder.Environment,
    builder.Configuration,
    authenticationOptions);

var signingKeyText = authenticationOptions.Jwt.SigningKey;
if (string.IsNullOrWhiteSpace(signingKeyText))
{
    signingKeyText = Convert.ToBase64String(
        RandomNumberGenerator.GetBytes(64));

    builder.Configuration[
        $"{AuthOptions.SectionName}:Jwt:SigningKey"] = signingKeyText;
}
else if (signingKeyText.Length < 32)
{
    throw new InvalidOperationException(
        "Authentication:Jwt:SigningKey must contain at least 32 characters.");
}

var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(signingKeyText));

builder.Services.Configure<AuthOptions>(authenticationSection);
builder.Services.AddSingleton<JwtTokenIssuer>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = authenticationOptions.Jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = authenticationOptions.Jwt.Audience,
            ValidateLifetime = true,
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(token)
                    && context.HttpContext.Request.Path
                        .StartsWithSegments("/hubs/projects"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

var requireAuthenticatedUser =
    authenticationOptions.RequireAuthenticatedUser;
var publicReadOnly =
    builder.Configuration.GetValue<bool?>("PublicAccess:ReadOnly") ?? false;

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ProjectRead, policy =>
    {
        // The hosted portfolio may expose the exact same production read model
        // to anonymous reviewers. No alternate demo backend is used.
        if (requireAuthenticatedUser && !publicReadOnly)
        {
            policy.RequireAuthenticatedUser();
        }
        else
        {
            policy.RequireAssertion(_ => true);
        }
    });

    options.AddPolicy(Policies.ProjectWrite, policy =>
    {
        // Public read-only always fails closed for writes.
        if (requireAuthenticatedUser || publicReadOnly)
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(Roles.Member, Roles.Admin);
        }
        else
        {
            policy.RequireAssertion(_ => true);
        }
    });

    options.AddPolicy(Policies.Admin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(Roles.Admin);
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
                    httpContext.Connection.RemoteIpAddress?.ToString()
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

await app.Services.InitializeDatabaseAsync();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseGitHubWebhookBodyBuffering();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/metrics"))
    {
        await next();
        return;
    }

    var metrics =
        context.RequestServices.GetRequiredService<ApiMetricsRegistry>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
        await next();
    }
    finally
    {
        metrics.Record(
            context.Request.Method,
            context.Response.StatusCode,
            stopwatch.Elapsed);
    }
});

var useHttpsRedirection =
    builder.Configuration.GetValue(
        "Hosting:UseHttpsRedirection",
        !app.Environment.IsDevelopment());

if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<ProjectEventsHub>("/hubs/projects");
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
