using System.Data;
using System.Diagnostics;
using System.Globalization;
using Dip.Api.Authorization;
using Dip.Application.Features.SystemStatus;
using Dip.Domain.Enums;
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

        var operations = await BuildContinuousOperationsAsync(cancellationToken);
        var databaseState = components.FirstOrDefault(x => x.Key == "database")?.State
            ?? SystemComponentStates.Unavailable;
        components.Add(new SystemComponentStatusDto(
            "coordination",
            "Durable Coordination",
            databaseState == SystemComponentStates.Healthy
                ? SystemComponentStates.Healthy
                : SystemComponentStates.Unavailable,
            databaseState == SystemComponentStates.Healthy
                ? "Queue coordination and delivery state are PostgreSQL-backed."
                : "Durable coordination cannot be confirmed while PostgreSQL is unavailable.",
            checkedAt));
        components.Add(new SystemComponentStatusDto(
            "queue-telemetry",
            "Queue Telemetry",
            operations.Queue.Available
                ? SystemComponentStates.Healthy
                : SystemComponentStates.NotConfigured,
            operations.Queue.Detail,
            checkedAt));

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
            missing.Length == 0
                ? $"Core native QA tools are available: {string.Join(", ", available)}."
                : $"Available: {(available.Length == 0 ? "none" : string.Join(", ", available))}. Missing optional/core tools: {string.Join(", ", missing)}.",
            checkedAt));

        var githubApp = configuration.GetValue<long>("GitHub:AppId") > 0
            && configuration.GetValue<long>("GitHub:InstallationId") > 0
            && (!string.IsNullOrWhiteSpace(configuration["GitHub:AppPrivateKeyBase64"]) || !string.IsNullOrWhiteSpace(configuration["GitHub:AppPrivateKeyPath"]));
        var token = !string.IsNullOrWhiteSpace(configuration["GitHub:Token"]);
        var oauth = !string.IsNullOrWhiteSpace(configuration["Authentication:GitHub:ClientId"]);
        components.Add(new SystemComponentStatusDto("github", "GitHub Repository Access",
            githubApp || token ? SystemComponentStates.Healthy : oauth ? SystemComponentStates.Degraded : SystemComponentStates.NotConfigured,
            githubApp
                ? "GitHub App repository connection configured."
                : token
                    ? "GitHub token repository connection configured."
                    : oauth
                        ? "OAuth sign-in is configured; repository API access is not."
                        : "GitHub repository access is not configured.",
            checkedAt));

        var core = components.Where(x => CriticalComponentKeys.Contains(x.Key));
        var overall = core.Any(x => x.State == SystemComponentStates.Unavailable)
            ? SystemComponentStates.Unavailable
            : core.Any(x => x.State == SystemComponentStates.Degraded)
                ? SystemComponentStates.Degraded
                : SystemComponentStates.Healthy;

        return new SystemStatusDto(overall, checkedAt, components)
        {
            Operations = operations
        };
    }

    private async Task<ContinuousOperationsDto> BuildContinuousOperationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var latestEvidence = await db.EvidenceRecords
                .AsNoTracking()
                .OrderByDescending(x => x.ObservedAtUtc)
                .Select(x => (DateTimeOffset?)x.ObservedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var latestTruth = await db.ProjectTruthSnapshots
                .AsNoTracking()
                .OrderByDescending(x => x.EvaluatedAtUtc)
                .Select(x => (DateTimeOffset?)x.EvaluatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var latestBackup = await db.QaBackupVerifications
                .AsNoTracking()
                .OrderByDescending(x => x.VerifiedAtUtc)
                .Select(x => (DateTimeOffset?)x.VerifiedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var queuedJobs = await db.DurableJobs
                .AsNoTracking()
                .LongCountAsync(x => x.Status == DurableJobStatus.Queued, cancellationToken);
            var runningJobs = await db.DurableJobs
                .AsNoTracking()
                .LongCountAsync(x => x.Status == DurableJobStatus.Running, cancellationToken);
            var failedJobs = await db.DurableJobs
                .AsNoTracking()
                .LongCountAsync(
                    x => x.Status == DurableJobStatus.Failed || x.Status == DurableJobStatus.TimedOut,
                    cancellationToken);

            var queue = await ReadQueueTransportTelemetryAsync(cancellationToken);

            return new ContinuousOperationsDto(
                true,
                latestEvidence,
                latestTruth,
                latestBackup,
                queuedJobs,
                runningJobs,
                failedJobs,
                queue,
                "Measured from PostgreSQL durable state at request time.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ContinuousOperationsDto(
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                new QueueTransportTelemetryDto(
                    false, null, null, null, null, null,
                    "Queue telemetry was not measured because durable-state telemetry failed."),
                $"Operational telemetry unavailable: {exception.Message}");
        }
    }

    private async Task<QueueTransportTelemetryDto> ReadQueueTransportTelemetryAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        try
        {
            if (openedHere)
                await connection.OpenAsync(cancellationToken);

            await using (var schemaCommand = connection.CreateCommand())
            {
                schemaCommand.CommandText = """
                    SELECT
                        to_regclass('dip_queue_messages') IS NOT NULL
                        AND to_regclass('dip_queue_deliveries') IS NOT NULL;
                    """;
                var schemaAvailable = Convert.ToBoolean(
                    await schemaCommand.ExecuteScalarAsync(cancellationToken),
                    CultureInfo.InvariantCulture);

                if (!schemaAvailable)
                {
                    return new QueueTransportTelemetryDto(
                        false, null, null, null, null, null,
                        "PostgreSQL queue tables have not been initialized in this runtime.");
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM dip_queue_messages) AS stored_envelopes,
                    (SELECT COUNT(*)
                       FROM dip_queue_deliveries
                      WHERE acknowledged_at_utc IS NULL
                        AND lease_until_utc > now()) AS active_leases,
                    (SELECT COUNT(*)
                       FROM dip_queue_messages
                      WHERE attempt > 0
                        AND NOT (
                            payload_json::jsonb ? 'failedAtUtc'
                            AND payload_json::jsonb ? 'sourceMessageId'
                        )) AS retry_envelopes,
                    (SELECT COUNT(*)
                       FROM dip_queue_messages
                      WHERE payload_json::jsonb ? 'failedAtUtc'
                        AND payload_json::jsonb ? 'sourceMessageId') AS dead_letter_envelopes,
                    EXTRACT(EPOCH FROM GREATEST(
                        (SELECT MAX(created_at_utc) FROM dip_queue_messages),
                        (SELECT MAX(acknowledged_at_utc) FROM dip_queue_deliveries)
                    )) AS latest_activity_epoch;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new QueueTransportTelemetryDto(
                    false, null, null, null, null, null,
                    "Queue telemetry query returned no row.");
            }

            DateTimeOffset? latestActivity = reader.IsDBNull(4)
                ? null
                : DateTimeOffset.FromUnixTimeMilliseconds(
                    checked((long)Math.Round(
                        Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture) * 1000d)));

            return new QueueTransportTelemetryDto(
                true,
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                latestActivity,
                "Measured directly from dip_queue_messages and dip_queue_deliveries; dead-letter envelopes are identified by the persisted failure envelope contract.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new QueueTransportTelemetryDto(
                false, null, null, null, null, null,
                $"Queue telemetry unavailable: {exception.Message}");
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<SystemComponentStatusDto>> BuildCriticalComponentsAsync(
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var analysisBase = configuration["AnalysisService:BaseUrl"] ?? "http://127.0.0.1:8100";
        var workerUrl = configuration["SystemStatus:WorkerMetricsUrl"] ?? "http://127.0.0.1:9091/metrics";
        return await Task.WhenAll(
            Task.FromResult(Healthy("api", "API", "HTTP pipeline is responding.", checkedAt)),
            ProbeAsync("database", "PostgreSQL", async token => (await db.Database.CanConnectAsync(token))
                ? (true, "Database and PostgreSQL-backed queue connection succeeded.")
                : (false, "Database connection check returned false."), checkedAt, cancellationToken),
            ProbeHttpAsync("analysis", "Python Analysis", new Uri(new Uri(EnsureTrailingSlash(analysisBase)), "health"), checkedAt, cancellationToken),
            ProbeHttpAsync("worker", "Worker", new Uri(workerUrl), checkedAt, cancellationToken),
            ProbeArtifactStoreAsync(checkedAt, cancellationToken));
    }

    private Task<SystemComponentStatusDto> ProbeHttpAsync(
        string key,
        string name,
        Uri uri,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
        => ProbeAsync(key, name, async token =>
        {
            using var client = httpClientFactory.CreateClient("system-status");
            using var response = await client.GetAsync(uri, token);
            return response.IsSuccessStatusCode
                ? (true, $"HTTP {(int)response.StatusCode} from {uri.Host}.")
                : (false, $"HTTP {(int)response.StatusCode} from {uri.Host}.");
        }, checkedAt, cancellationToken);

    private Task<SystemComponentStatusDto> ProbeArtifactStoreAsync(
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var provider = configuration["Artifacts:Provider"] ?? "Local";
        if (provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration["Artifacts:Minio:Endpoint"] ?? "http://127.0.0.1:9000";
            return ProbeHttpAsync(
                "artifact-store",
                "Artifact Storage",
                new Uri(new Uri(EnsureTrailingSlash(endpoint)), "minio/health/live"),
                checkedAt,
                cancellationToken);
        }

        var root = configuration["Artifacts:LocalRoot"] ?? "artifacts/durable";
        return ProbeAsync("artifact-store", "Artifact Storage", token =>
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
        }, checkedAt, cancellationToken);
    }

    private async Task<SystemComponentStatusDto> ProbeAsync(
        string key,
        string name,
        Func<CancellationToken, Task<(bool Healthy, string Detail)>> probe,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Clamp(configuration.GetValue<int?>("SystemStatus:ProbeTimeoutSeconds") ?? 4, 1, 30);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await probe(timeout.Token);
            sw.Stop();
            return new SystemComponentStatusDto(
                key,
                name,
                result.Healthy ? SystemComponentStates.Healthy : SystemComponentStates.Unavailable,
                result.Detail,
                checkedAt,
                Math.Round(sw.Elapsed.TotalMilliseconds, 2));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new SystemComponentStatusDto(
                key,
                name,
                SystemComponentStates.Unavailable,
                $"Health probe timed out after {timeoutSeconds} s.",
                checkedAt,
                sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            sw.Stop();
            return new SystemComponentStatusDto(
                key,
                name,
                SystemComponentStates.Unavailable,
                exception.Message,
                checkedAt,
                sw.Elapsed.TotalMilliseconds);
        }
    }

    private static SystemComponentStatusDto Healthy(
        string key,
        string name,
        string detail,
        DateTimeOffset checkedAt)
        => new(key, name, SystemComponentStates.Healthy, detail, checkedAt, 0d);

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
}
