// Purpose: Implements System Integration QA Pack infrastructure behavior for quality automation.
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Npgsql;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Opt-in API/DB/distributed-system QA. Contracts are repository-owned in
/// .dip/system-qa.json. Secrets are referenced by environment-variable name and
/// are never written to artifacts.
/// </summary>
public sealed class SystemIntegrationQaPack : ICrossCuttingQaPack, IDisposable
{
    private readonly HttpClient _http = new(); public string PackId => "system-integration";
    public bool Applies(QaTechnologyProfile profile, string workspace) => File.Exists(Path.Combine(workspace, ".dip", "system-qa.json"));
    public async Task<QaCrossCuttingResult> RunAsync(QaExecutionCommand command, QaTechnologyProfile profile, string workspace, string artifactRoot, CancellationToken ct)
    {
        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var path = Path.Combine(workspace, ".dip", "system-qa.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        var root = doc.RootElement;
        var allowed = ReadStrings(root, "allowRemoteHosts").ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("http", out var http) && http.ValueKind == JsonValueKind.Array)
            foreach (var item in http.EnumerateArray().Take(100)) await CheckHttp(item, allowed, false);
        if (root.TryGetProperty("distributed",
            out var dist) && dist.ValueKind == JsonValueKind.Array) foreach (var item in dist.EnumerateArray().Take(50)) await CheckHttp(item,
            allowed,
            true);
        if (root.TryGetProperty("database", out var db) && db.ValueKind == JsonValueKind.Array) foreach (var item in db.EnumerateArray().Take(50)) await CheckDatabase(item);
        Directory.CreateDirectory(artifactRoot);
        var summary = JsonSerializer.Serialize(new { pack = PackId, tools = tools.Select(x => new { x.Tool, x.Succeeded, x.DurationMs, x.Summary }), findingCount = findings.Count });
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "system-integration-summary.json"), summary, ct);
        return new(PackId, tools, findings, summary);

        async Task CheckHttp(JsonElement item, HashSet<string> allow, bool repeated)
        {
            var name = Text(item, "name") ?? "http";
            var raw = Text(item, "url");
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                findings.Add(Finding("api", Severity.Critical, name, "Invalid API QA URL", $"'{raw}' is not an absolute URL.", "DIP-API-CONTRACT"));
                return;
            }
            if (!IsAllowed(uri, allow))
            {
                findings.Add(Finding("api", Severity.Critical, name, "Remote host is not allowlisted", $"{uri.Host} must be listed in allowRemoteHosts before DIP will issue a QA request.", "DIP-API-HOST"));
                return;
            }
            var expected = Number(item, "expectedStatus") ?? 200;
            var attempts = repeated ? Math.Clamp(Number(item, "attempts") ?? 3, 2, 20) : 1;
            var timeoutMs = Math.Clamp(Number(item, "timeoutMs") ?? 5000, 100, 30000);
            var failures = 0;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < attempts; i++)
            {
                using var req = new HttpRequestMessage(new HttpMethod(Text(item, "method") ?? "GET"), uri);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(timeoutMs);
                try
                {
                    using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    if ((int)res.StatusCode != expected)
                    {
                        failures++;
                        findings.Add(Finding(repeated ? "distributed-system" : "api", Severity.Warning, name, $"Unexpected HTTP status from {name}", $"Expected {expected}, received {(int)res.StatusCode} on attempt {i + 1}.", "DIP-HTTP-STATUS"));
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    failures++;
                    findings.Add(Finding(repeated ? "distributed-system" : "api", Severity.Critical, name, $"HTTP probe failed for {name}", $"Attempt {i + 1}: {ex.GetType().Name}: {ex.Message}", "DIP-HTTP-FAIL"));
                }
            }
            sw.Stop();
            var succeeded = failures == 0;
            tools.Add(new($"http:{name}", true, succeeded, succeeded ? 0 : 1, sw.ElapsedMilliseconds, "", "", $"{attempts - failures}/{attempts} HTTP probe(s) matched expected status."));
            if (repeated && failures > 0 && failures < attempts) findings.Add(Finding("distributed-system", Severity.Warning, name, "Intermittent distributed-system health response", $"{failures}/{attempts} probes failed; investigate retry, timeout, load-balancing or dependency health.", "DIP-DIST-FLAKY"));
        }
        async Task CheckDatabase(JsonElement item)
        {
            var name = Text(item, "name") ?? "database";
            var provider = Text(item, "provider") ?? "postgres";
            var envName = Text(item, "connectionStringEnvironment");
            if (!provider.Equals("postgres", StringComparison.OrdinalIgnoreCase) && !provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Finding("database", Severity.Warning, name, "Database provider requires a custom QA adapter", $"Provider '{provider}' is not built in; configure a no-shell command in .dip/qa.json.", "DIP-DB-PROVIDER"));
                return;
            }
            if (string.IsNullOrWhiteSpace(envName))
            {
                findings.Add(Finding("database", Severity.Critical, name, "Database secret binding is missing", "connectionStringEnvironment is required; connection strings must not be committed to system-qa.json.", "DIP-DB-SECRET"));
                return;
            }
            var connection = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(connection))
            {
                tools.Add(new($"db:{name}", false, false, null, 0, "", "", $"Environment variable {envName} is unavailable; DB probe skipped."));
                return;
            }
            var query = Text(item, "probeQuery") ?? "SELECT 1";
            if (!query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Finding("database", Severity.Critical, name, "Unsafe DB QA probe rejected", "Only read-only SELECT probes are accepted by the built-in DB pack.", "DIP-DB-READONLY"));
                return;
            }
            var sw = Stopwatch.StartNew();
            try
            {
                var timeoutSeconds = Math.Clamp((Number(item, "timeoutSeconds") ?? 5), 1, 30);
                var builder = new NpgsqlConnectionStringBuilder(connection) { Timeout = timeoutSeconds };
                await using var conn = new NpgsqlConnection(builder.ConnectionString);
                await conn.OpenAsync(ct);
                await using var cmd = new NpgsqlCommand(query, conn) { CommandTimeout = timeoutSeconds };
                _ = await cmd.ExecuteScalarAsync(ct);
                sw.Stop();
                tools.Add(new($"db:{name}", true, true, 0, sw.ElapsedMilliseconds, "", "", "PostgreSQL read-only probe succeeded."));
            }
            catch (Exception ex)
            {
                sw.Stop();
                tools.Add(new($"db:{name}", true, false, 1, sw.ElapsedMilliseconds, "", "", "PostgreSQL probe failed."));
                findings.Add(Finding("database", Severity.Critical, name, "Database contract probe failed", ex.GetType().Name + ": " + ex.Message, "DIP-DB-PROBE"));
            }
        }
    }
    private static bool IsAllowed(Uri uri, HashSet<string> allow) => IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip) || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || allow.Contains(uri.Host);
    private static string? Text(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int? Number(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.TryGetInt32(out var x) ? x : null;
    private static IEnumerable<string> ReadStrings(JsonElement e, string n) => e.TryGetProperty(n, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)) : [];
    private static QaFindingCandidate Finding(string category, Severity severity, string tool, string title, string message, string rule) => new(category, severity, "dip-system-qa:" + tool, title, message, RuleId: rule, SourceFormat: "dip-system-qa", SourceFormatVersion: "1.0"); public void Dispose() => _http.Dispose();
}
