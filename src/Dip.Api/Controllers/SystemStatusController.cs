using System.Diagnostics;
using Dip.Api.Authorization;
using Dip.Application.Features.SystemStatus;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.QualityAutomation;
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
    private static readonly HashSet<string> CriticalComponentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "database",
        "redis",
        "analysis",
        "worker",
        "artifact-store"
    };

    [AllowAnonymous]
    [HttpGet("live")]
    [ProducesResponseType(typeof(SystemLivenessDto), StatusCodes.Status200OK)]
    public ActionResult<SystemLivenessDto> GetLiveness()
    {
        var checkedAt = DateTimeOffset.UtcNow;
        return Ok(new SystemLivenessDto(true, "Live", checkedAt));
    }

    /// <summary>
    /// Dependency-aware traffic-admission probe. Only critical runtime dependencies are
    /// touched here; toolchain discovery and integration diagnostics belong to /status.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("ready")]
    [ProducesResponseType(typeof(SystemReadinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SystemReadinessDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemReadinessDto>> GetReadiness(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var critical = await BuildCriticalComponentsAsync(checkedAt, cancellationToken);
        var unavailable = critical
            .Where(component => component.State == SystemComponentStates.Unavailable)
            .Select(component => component.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        var degraded = critical
            .Where(component => component.State == SystemComponentStates.Degraded)
            .Select(component => component.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var ready = unavailable.Length == 0;
        var dto = new SystemReadinessDto(
            ready,
            ready ? "Ready" : "NotReady",
            checkedAt,
            unavailable,
            degraded);

        return ready ? Ok(dto) : StatusCode(StatusCodes.Status503ServiceUnavailable, dto);
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("status")]
    [ProducesResponseType(typeof(SystemStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await BuildStatusAsync(cancellationToken));
    }

    private async Task<SystemStatusDto> BuildStatusAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var components = (await BuildCriticalComponentsAsync(checkedAt, cancellationToken)).ToList();

        var sandboxEnabled = configuration.GetValue<bool?>("Qa:SandboxEnabled") ?? true;
        components.Add(new SystemComponentStatusDto(
            "qa-sandbox",
            "QA Sandbox",
            sandboxEnabled ? SystemComponentStates.Healthy : SystemComponentStates.Degraded,
            sandboxEnabled
                ? "Sandbox boundary is enabled."
                : "Sandbox boundary is disabled; QA execution is not isolated.",
            checkedAt));

        var qaTools = new[]
        {
            (Name: "dotnet", Candidates: new[] { "dotnet" }),
            (Name: "python", Candidates: new[] { "python3", "python" }),
            (Name: "cmake", Candidates: new[] { "cmake" }),
            (Name: "clang-tidy", Candidates: new[] { "clang-tidy" }),
            (Name: "cppcheck", Candidates: new[] { "cppcheck" })
        };
        var availableQaTools = qaTools
            .Where(tool => tool.Candidates.Any(candidate => ProcessExecutor.FindExecutable(candidate) is not null))
            .Select(tool => tool.Name)
            .ToArray();
        var missingQaTools = qaTools
            .Where(tool => tool.Candidates.All(candidate => ProcessExecutor.FindExecutable(candidate) is null))
            .Select(tool => tool.Name)
            .ToArray();
        components.Add(new SystemComponentStatusDto(
            "qa-toolchain",
            "QA Toolchain",
            missingQaTools.Length == 0 ? SystemComponentStates.Healthy : SystemComponentStates.Degraded,
            missingQaTools.Length == 0
                ? $"Core repository QA tools are available: {string.Join(", ", availableQaTools)}."
                : $"Available: {(availableQaTools.Length == 0 ? "none" : string.Join(", ", availableQaTools))}. Missing core tools: {string.Join(", ", missingQaTools)}. Rebuild the API image with the real-use QA toolchain before relying on repository-wide QA.",
            checkedAt));

        var githubAppConfigured =
            configuration.GetValue<long>("GitHub:AppId") > 0
            && configuration.GetValue<long>("GitHub:InstallationId") > 0
            && (!string.IsNullOrWhiteSpace(configuration["GitHub:AppPrivateKeyBase64"])
                || !string.IsNullOrWhiteSpace(configuration["GitHub:AppPrivateKeyPath"]));
        var githubTokenConfigured = !string.IsNullOrWhiteSpace(configuration["GitHub:Token"]);
        var githubOAuthConfigured = !string.IsNullOrWhiteSpace(configuration["Authentication:GitHub:ClientId"]);
        var repositoryConnectionConfigured = githubAppConfigured || githubTokenConfigured;
        var githubMode = githubAppConfigured
            ? "GitHub App repository connection"
            : githubTokenConfigured
                ? "GitHub token repository connection"
                : githubOAuthConfigured ? "GitHub OAuth sign-in only" : "none";
        components.Add(new SystemComponentStatusDto(
            "github",
            "GitHub Repository Access",
            repositoryConnectionConfigured
                ? SystemComponentStates.Healthy
                : githubOAuthConfigured ? SystemComponentStates.Degraded : SystemComponentStates.NotConfigured,
            repositoryConnectionConfigured
                ? $"Configured mode: {githubMode}. Use Settings > Integrations or the Repository Picker for a live capability check."
                : githubOAuthConfigured
                    ? "GitHub OAuth sign-in is configured, but repository API access is not. Set GitHub:Token or GitHub App installation credentials to import and synchronize repositories."
                    : "GitHub repository access is not configured. Set a read-only GitHub token or GitHub App installation to browse, import and synchronize repositories.",
            checkedAt));

        var core = components.Where(component => CriticalComponentKeys.Contains(component.Key));
        var overall = core.Any(component => component.State == SystemComponentStates.Unavailable)
            ? SystemComponentStates.Unavailable
            : core.Any(component => component.State == SystemComponentStates.Degraded)
                ? SystemComponentStates.Degraded
                : SystemComponentStates.Healthy;

        return new SystemStatusDto(overall, checkedAt, components);
    }

    private async Task<IReadOnlyList<SystemComponentStatusDto>> BuildCriticalComponentsAsync(
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var analysisBase = configuration["AnalysisService:BaseUrl"] ?? "http://127.0.0.1:8100";
        var workerUrl = configuration["SystemStatus:WorkerMetricsUrl"] ?? "http://127.0.0.1:9091/metrics";

        var probes = new Task<SystemComponentStatusDto>[]
        {
            Task.FromResult(Healthy("api", "API", "HTTP pipeline is responding.", checkedAt)),
            ProbeAsync(
                "database",
                "PostgreSQL",
                async token => (await db.Database.CanConnectAsync(token))
                    ? (true, "Database connection succeeded.")
                    : (false, "Database connection check returned false."),
                checkedAt,
                cancellationToken),
            ProbeAsync(
                "redis",
                "Redis",
                async token =>
                {
                    token.ThrowIfCancellationRequested();
                    var latency = await redis.GetDatabase().PingAsync();
                    return (true, $"Ping {latency.TotalMilliseconds:0.#} ms.");
                },
                checkedAt,
                cancellationToken),
            ProbeHttpAsync(
                "analysis",
                "Python Analysis",
                new Uri(new Uri(EnsureTrailingSlash(analysisBase)), "health"),
                checkedAt,
                cancellationToken),
            ProbeHttpAsync("worker", "Worker", new Uri(workerUrl), checkedAt, cancellationToken),
            ProbeArtifactStoreAsync(checkedAt, cancellationToken)
        };

        return await Task.WhenAll(probes);
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

    private async Task<SystemComponentStatusDto> ProbeAsync(
        string key,
        string name,
        Func<CancellationToken, Task<(bool Healthy, string Detail)>> probe,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Clamp(
            configuration.GetValue<int?>("SystemStatus:ProbeTimeoutSeconds") ?? 4,
            1,
            30);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await probe(timeout.Token);
            stopwatch.Stop();
            return new SystemComponentStatusDto(
                key,
                name,
                result.Healthy ? SystemComponentStates.Healthy : SystemComponentStates.Unavailable,
                result.Detail,
                checkedAt,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new SystemComponentStatusDto(
                key,
                name,
                SystemComponentStates.Unavailable,
                $"Health probe timed out after {timeoutSeconds} s.",
                checkedAt,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new SystemComponentStatusDto(
                key,
                name,
                SystemComponentStates.Unavailable,
                exception.Message,
                checkedAt,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static SystemComponentStatusDto Healthy(
        string key,
        string name,
        string detail,
        DateTimeOffset checkedAt) =>
        new(key, name, SystemComponentStates.Healthy, detail, checkedAt, 0d);

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
}
