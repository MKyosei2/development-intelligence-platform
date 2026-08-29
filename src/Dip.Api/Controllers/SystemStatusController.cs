using System.Diagnostics;
using Dip.Api.Authorization;
using Dip.Application.Features.SystemStatus;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.QualityAutomation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemStatusController(
    DipDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ControllerBase
{
    private static readonly HashSet<string> CriticalComponentKeys = new(StringComparer.OrdinalIgnoreCase)
    { "api", "database", "analysis", "worker", "artifact-store" };

    [AllowAnonymous, HttpGet("live")]
    public ActionResult<SystemLivenessDto> GetLiveness()
        => Ok(new SystemLivenessDto(true, "Live", DateTimeOffset.UtcNow));

    [AllowAnonymous, HttpGet("ready")]
    [ProducesResponseType(typeof(SystemReadinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SystemReadinessDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemReadinessDto>> GetReadiness(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var critical = await BuildCriticalComponentsAsync(checkedAt, cancellationToken);
        var unavailable = critical.Where(x => x.State == SystemComponentStates.Unavailable).Select(x => x.Key).OrderBy(x => x).ToArray();
        var degraded = critical.Where(x => x.State == SystemComponentStates.Degraded).Select(x => x.Key).OrderBy(x => x).ToArray();
        var ready = unavailable.Length == 0;
        var dto = new SystemReadinessDto(ready, ready ? "Ready" : "NotReady", checkedAt, unavailable, degraded);
        return ready ? Ok(dto) : StatusCode(StatusCodes.Status503ServiceUnavailable, dto);
    }

    [Authorize(Policy = Policies.ProjectRead), HttpGet("status")]
    public async Task<ActionResult<SystemStatusDto>> GetStatus(CancellationToken cancellationToken)
        => Ok(await BuildStatusAsync(cancellationToken));

    private async Task<SystemStatusDto> BuildStatusAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var components = (await BuildCriticalComponentsAsync(checkedAt, cancellationToken)).ToList();
        components.Add(new SystemComponentStatusDto(
            "coordination", "Durable Coordination", SystemComponentStates.Healthy,
            "Queue coordination is PostgreSQL-backed; no Redis daemon is required.", checkedAt));

        var sandboxEnabled = configuration.GetValue<bool?>("Qa:SandboxEnabled") ?? true;
        components.Add(new SystemComponentStatusDto("qa-sandbox", "QA Sandbox",
            sandboxEnabled ? SystemComponentStates.Healthy : SystemComponentStates.Degraded,
            sandboxEnabled ? "Native process sandbox boundary is enabled." : "Sandbox boundary is disabled.", checkedAt));

        var qaTools = new[]
        {
            (Name: "dotnet", Candidates: new[] { "dotnet" }),
            (Name: "python", Candidates: new[] { "python3", "python" }),
            (Name: "cmake", Candidates: new[] { "cmake" }),
            (Name: "clang-tidy", Candidates: new[] { "clang-tidy" }),
            (Name: "cppcheck", Candidates: new[] { "cppcheck" })
        };
        var available = qaTools.Where(t => t.Candidates.Any(c => ProcessExecutor.FindExecutable(c) is not null)).Select(t => t.Name).ToArray();
        var missing = qaTools.Where(t => t.Candidates.All(c => ProcessExecutor.FindExecutable(c) is null)).Select(t => t.Name).ToArray();
        components.Add(new SystemComponentStatusDto("qa-toolchain", "QA Toolchain",
            missing.Length == 0 ? SystemComponentStates.Healthy : SystemComponentStates.Degraded,
            missing.Length == 0 ? $"Core native QA tools are available: {string.Join(", ", available)}."
                : $"Available: {(available.Length == 0 ? "none" : string.Join(", ", available))}. Missing optional/core tools: {string.Join(", ", missing)}.", checkedAt));

        var githubApp = configuration.GetValue<long>("GitHub:AppId") > 0
            && configuration.GetValue<long>("GitHub:InstallationId") > 0
            && (!string.IsNullOrWhiteSpace(configuration["GitHub:AppPrivateKeyBase64"]) || !string.IsNullOrWhiteSpace(configuration["GitHub:AppPrivateKeyPath"]));
        var token = !string.IsNullOrWhiteSpace(configuration["GitHub:Token"]);
        var oauth = !string.IsNullOrWhiteSpace(configuration["Authentication:GitHub:ClientId"]);
        components.Add(new SystemComponentStatusDto("github", "GitHub Repository Access",
            githubApp || token ? SystemComponentStates.Healthy : oauth ? SystemComponentStates.Degraded : SystemComponentStates.NotConfigured,
            githubApp ? "GitHub App repository connection configured." : token ? "GitHub token repository connection configured." : oauth ? "OAuth sign-in is configured; repository API access is not." : "GitHub repository access is not configured.", checkedAt));

        var core = components.Where(x => CriticalComponentKeys.Contains(x.Key));
        var overall = core.Any(x => x.State == SystemComponentStates.Unavailable) ? SystemComponentStates.Unavailable
            : core.Any(x => x.State == SystemComponentStates.Degraded) ? SystemComponentStates.Degraded : SystemComponentStates.Healthy;
        return new SystemStatusDto(overall, checkedAt, components);
    }

    private async Task<IReadOnlyList<SystemComponentStatusDto>> BuildCriticalComponentsAsync(DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        var analysisBase = configuration["AnalysisService:BaseUrl"] ?? "http://127.0.0.1:8100";
        var workerUrl = configuration["SystemStatus:WorkerMetricsUrl"] ?? "http://127.0.0.1:9091/metrics";
        return await Task.WhenAll(
            Task.FromResult(Healthy("api", "API", "HTTP pipeline is responding.", checkedAt)),
            ProbeAsync("database", "PostgreSQL", async token => (await db.Database.CanConnectAsync(token))
                ? (true, "Database and PostgreSQL-backed queue connection succeeded.") : (false, "Database connection check returned false."), checkedAt, cancellationToken),
            ProbeHttpAsync("analysis", "Python Analysis", new Uri(new Uri(EnsureTrailingSlash(analysisBase)), "health"), checkedAt, cancellationToken),
            ProbeHttpAsync("worker", "Worker", new Uri(workerUrl), checkedAt, cancellationToken),
            ProbeArtifactStoreAsync(checkedAt, cancellationToken));
    }

    private Task<SystemComponentStatusDto> ProbeHttpAsync(string key, string name, Uri uri, DateTimeOffset checkedAt, CancellationToken cancellationToken)
        => ProbeAsync(key, name, async token =>
        {
            using var client = httpClientFactory.CreateClient("system-status");
            using var response = await client.GetAsync(uri, token);
            return response.IsSuccessStatusCode ? (true, $"HTTP {(int)response.StatusCode} from {uri.Host}.") : (false, $"HTTP {(int)response.StatusCode} from {uri.Host}.");
        }, checkedAt, cancellationToken);

    private Task<SystemComponentStatusDto> ProbeArtifactStoreAsync(DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        var provider = configuration["Artifacts:Provider"] ?? "Local";
        if (provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration["Artifacts:Minio:Endpoint"] ?? "http://127.0.0.1:9000";
            return ProbeHttpAsync(
                "artifact-store", "Artifact Storage",
                new Uri(new Uri(EnsureTrailingSlash(endpoint)), "minio/health/live"), checkedAt, cancellationToken);
        }

        var root = configuration["Artifacts:LocalRoot"] ?? "artifacts/durable";
        return ProbeAsync("artifact-store", "Artifact Storage", token =>
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".dip-health-{Guid.NewGuid():N}.tmp");
            try { System.IO.File.WriteAllText(probe, "ok"); return Task.FromResult((true, $"Local storage is writable: {Path.GetFullPath(root)}")); }
            finally { if (System.IO.File.Exists(probe)) System.IO.File.Delete(probe); }
        }, checkedAt, cancellationToken);
    }

    private async Task<SystemComponentStatusDto> ProbeAsync(string key, string name, Func<CancellationToken, Task<(bool Healthy, string Detail)>> probe, DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Clamp(configuration.GetValue<int?>("SystemStatus:ProbeTimeoutSeconds") ?? 4, 1, 30);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await probe(timeout.Token); sw.Stop();
            return new SystemComponentStatusDto(key, name, result.Healthy ? SystemComponentStates.Healthy : SystemComponentStates.Unavailable, result.Detail, checkedAt, Math.Round(sw.Elapsed.TotalMilliseconds, 2));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { sw.Stop(); return new SystemComponentStatusDto(key, name, SystemComponentStates.Unavailable, $"Health probe timed out after {timeoutSeconds} s.", checkedAt, sw.Elapsed.TotalMilliseconds); }
        catch (Exception ex)
        { sw.Stop(); return new SystemComponentStatusDto(key, name, SystemComponentStates.Unavailable, ex.Message, checkedAt, sw.Elapsed.TotalMilliseconds); }
    }

    private static SystemComponentStatusDto Healthy(string key, string name, string detail, DateTimeOffset checkedAt)
        => new(key, name, SystemComponentStates.Healthy, detail, checkedAt, 0d);
    private static string EnsureTrailingSlash(string value) => value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
}
