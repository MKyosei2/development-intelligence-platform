using Dip.Api.Authorization;
using Dip.Application.Features.SystemStatus;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemStatusController(
    DipDbContext db,
    IConnectionMultiplexer redis,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("status")]
    public async Task<ActionResult<SystemStatusDto>> GetStatus(
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var components = new List<SystemComponentStatusDto>
        {
            Healthy("api", "API", "HTTP pipeline is responding.", checkedAt)
        };

        components.Add(await ProbeAsync(
            "database",
            "PostgreSQL",
            async token => (await db.Database.CanConnectAsync(token))
                ? (true, "Database connection succeeded.")
                : (false, "Database connection check returned false."),
            checkedAt,
            cancellationToken));

        components.Add(await ProbeAsync(
            "redis",
            "Redis",
            async token =>
            {
                token.ThrowIfCancellationRequested();
                var latency = await redis.GetDatabase().PingAsync();
                return (true, $"Ping {latency.TotalMilliseconds:0.#} ms.");
            },
            checkedAt,
            cancellationToken));

        var analysisBase = configuration["AnalysisService:BaseUrl"]
            ?? "http://127.0.0.1:8100";
        components.Add(await ProbeHttpAsync(
            "analysis",
            "Python Analysis",
            new Uri(new Uri(EnsureTrailingSlash(analysisBase)), "health"),
            checkedAt,
            cancellationToken));

        var workerUrl = configuration["SystemStatus:WorkerMetricsUrl"]
            ?? "http://127.0.0.1:9091/metrics";
        components.Add(await ProbeHttpAsync(
            "worker",
            "Worker",
            new Uri(workerUrl),
            checkedAt,
            cancellationToken));

        components.Add(await ProbeArtifactStoreAsync(checkedAt, cancellationToken));

        var sandboxEnabled = configuration.GetValue<bool?>("Qa:SandboxEnabled") ?? true;
        components.Add(new SystemComponentStatusDto(
            "qa-sandbox",
            "QA Sandbox",
            sandboxEnabled ? SystemComponentStates.Healthy : SystemComponentStates.Degraded,
            sandboxEnabled
                ? "Sandbox boundary is enabled."
                : "Sandbox boundary is disabled; QA execution is not isolated.",
            checkedAt));

        var githubConfigured =
            !string.IsNullOrWhiteSpace(configuration["Authentication:GitHub:ClientId"])
            || !string.IsNullOrWhiteSpace(configuration["GitHub:Token"]);
        components.Add(new SystemComponentStatusDto(
            "github",
            "GitHub",
            githubConfigured
                ? SystemComponentStates.Healthy
                : SystemComponentStates.NotConfigured,
            githubConfigured
                ? "GitHub credentials are configured."
                : "GitHub OAuth/token is not configured. Local development can still run.",
            checkedAt));

        var core = components.Where(x => x.Key is
            "api" or "database" or "redis" or "analysis" or "worker" or "artifact-store");
        var overall = core.Any(x => x.State == SystemComponentStates.Unavailable)
            ? SystemComponentStates.Degraded
            : core.Any(x => x.State == SystemComponentStates.Degraded)
                ? SystemComponentStates.Degraded
                : SystemComponentStates.Healthy;

        return Ok(new SystemStatusDto(overall, checkedAt, components));
    }

    private async Task<SystemComponentStatusDto> ProbeHttpAsync(
        string key,
        string name,
        Uri uri,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        return await ProbeAsync(
            key,
            name,
            async token =>
            {
                using var client = httpClientFactory.CreateClient("system-status");
                using var response = await client.GetAsync(uri, token);
                return response.IsSuccessStatusCode
                    ? (true, $"HTTP {(int)response.StatusCode} from {uri.Host}.")
                    : (false, $"HTTP {(int)response.StatusCode} from {uri.Host}.");
            },
            checkedAt,
            cancellationToken);
    }

    private async Task<SystemComponentStatusDto> ProbeArtifactStoreAsync(
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var provider = configuration["Artifacts:Provider"] ?? "Local";
        if (provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration["Artifacts:Minio:Endpoint"]
                ?? "http://127.0.0.1:9000";
            return await ProbeHttpAsync(
                "artifact-store",
                "Artifact Storage",
                new Uri(new Uri(EnsureTrailingSlash(endpoint)), "minio/health/live"),
                checkedAt,
                cancellationToken);
        }

        var root = configuration["Artifacts:LocalRoot"] ?? "artifacts/durable";
        return await ProbeAsync(
            "artifact-store",
            "Artifact Storage",
            token =>
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(root);
                var probe = Path.Combine(root, $".dip-health-{Guid.NewGuid():N}.tmp");
                try
                {
                    System.IO.File.WriteAllText(probe, "ok");
                    return Task.FromResult((true, $"Local storage is writable: {Path.GetFullPath(root)}"));
                }
                finally
                {
                    if (System.IO.File.Exists(probe))
                        System.IO.File.Delete(probe);
                }
            },
            checkedAt,
            cancellationToken);
    }

    private static async Task<SystemComponentStatusDto> ProbeAsync(
        string key,
        string name,
        Func<CancellationToken, Task<(bool Healthy, string Detail)>> probe,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            var result = await probe(timeout.Token);
            return new SystemComponentStatusDto(
                key,
                name,
                result.Healthy ? SystemComponentStates.Healthy : SystemComponentStates.Unavailable,
                result.Detail,
                checkedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SystemComponentStatusDto(
                key, name, SystemComponentStates.Unavailable, "Health probe timed out.", checkedAt);
        }
        catch (Exception exception)
        {
            return new SystemComponentStatusDto(
                key, name, SystemComponentStates.Unavailable, exception.Message, checkedAt);
        }
    }

    private static SystemComponentStatusDto Healthy(
        string key,
        string name,
        string detail,
        DateTimeOffset checkedAt) =>
        new(key, name, SystemComponentStates.Healthy, detail, checkedAt);

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
}
