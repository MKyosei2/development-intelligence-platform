// Purpose: Runs deterministic multi-client game QA using an explicit telemetry contract, impairment attestation, and state-hash desync detection.
using System.Globalization;
using System.Text.Json;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

public sealed record MultiplayerQaCommand(
    ExperimentTarget Target,
    int Clients = 2,
    int DurationSeconds = 60,
    int LatencyMs = 0,
    int JitterMs = 0,
    decimal PacketLossPercent = 0m,
    int MinimumCommonTicks = 10,
    int MaximumDesyncTicks = 0);

public sealed record MultiplayerStateSample(
    int Client,
    long Tick,
    string StateHash,
    decimal? RttMs,
    decimal? ObservedPacketLossPercent,
    bool NetworkImpairmentApplied);

public sealed record MultiplayerQaResult(
    string Status,
    int Clients,
    int Samples,
    int CommonTicks,
    int DesyncTicks,
    decimal? AverageRttMs,
    decimal? AverageObservedPacketLossPercent,
    bool NetworkImpairmentVerified,
    string ArtifactReference,
    string Summary);

public sealed record MultiplayerTelemetryAssessment(
    string Status,
    int Samples,
    int CommonTicks,
    int DesyncTicks,
    decimal? AverageRttMs,
    decimal? AverageObservedPacketLossPercent,
    bool NetworkImpairmentVerified,
    string Summary);

public interface IMultiplayerQaExperimentRunner
{
    Task<MultiplayerQaResult> RunAsync(MultiplayerQaCommand command, CancellationToken ct);
}

/// <summary>
/// The configured executable is a multiplayer QA harness. It owns all clients inside
/// one isolated execution environment so loopback/private in-sandbox networking can be
/// used without exposing the worker host. A requested latency/jitter/loss condition is
/// never assumed to have been applied: each client must attest it in telemetry.
/// </summary>
public sealed class MultiplayerQaExperimentRunner(
    ProcessExecutor executor,
    IQaWorkspaceResolver workspaceResolver) : IMultiplayerQaExperimentRunner
{
    public const string TelemetryPrefix = "DIP_MP_STATE";
    public const string TelemetrySchema = "dip.multiplayer-qa.v1";

    public async Task<MultiplayerQaResult> RunAsync(MultiplayerQaCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var clients = Math.Clamp(command.Clients, 2, 32);
        var duration = Math.Clamp(command.DurationSeconds, 1, 7200);
        var latency = Math.Clamp(command.LatencyMs, 0, 10_000);
        var jitter = Math.Clamp(command.JitterMs, 0, 10_000);
        var loss = Math.Clamp(command.PacketLossPercent, 0m, 100m);
        var workspace = workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable = ResolveExecutable(workspace, command.Target.Executable);
        var root = Path.Combine(
            Path.GetFullPath("artifacts"),
            "p2",
            command.Target.ProjectId.ToString("N"),
            "multiplayer",
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var stdout = Path.Combine(root, "stdout.log");
        var stderr = Path.Combine(root, "stderr.log");

        var arguments = (command.Target.Arguments ?? Array.Empty<string>())
            .Select(x => x
                .Replace("{clients}", clients.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{duration_seconds}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{latency_ms}", latency.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{jitter_ms}", jitter.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{packet_loss_percent}", loss.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            .ToArray();
        var environment = new Dictionary<string, string>
        {
            ["DIP_MP_QA"] = "1",
            ["DIP_MP_TELEMETRY_FORMAT"] = "v1",
            ["DIP_MP_CLIENT_COUNT"] = clients.ToString(CultureInfo.InvariantCulture),
            ["DIP_MP_DURATION_SECONDS"] = duration.ToString(CultureInfo.InvariantCulture),
            ["DIP_NET_LATENCY_MS"] = latency.ToString(CultureInfo.InvariantCulture),
            ["DIP_NET_JITTER_MS"] = jitter.ToString(CultureInfo.InvariantCulture),
            ["DIP_NET_PACKET_LOSS_PERCENT"] = loss.ToString(CultureInfo.InvariantCulture),
            ["DIP_MP_REQUIRE_NETWORK_IMPAIRMENT_ACK"] = "1"
        };

        var execution = await executor.RunQaAsync(
            executable,
            arguments,
            workspace,
            environment,
            TimeSpan.FromSeconds(Math.Max(duration + 30, Math.Clamp(command.Target.TimeoutSeconds, 1, 7200))),
            stdout,
            stderr,
            ct);

        if (execution.TimedOut || execution.ExitCode is not 0)
        {
            var status = execution.ExitCode is null && !execution.TimedOut ? "InfrastructureFailure" : "Failed";
            var summary = execution.TimedOut
                ? "Multiplayer QA harness timed out."
                : $"Multiplayer QA harness exited with code {(execution.ExitCode?.ToString() ?? "n/a")}.";
            await WriteEvidenceAsync(root, command, execution, Array.Empty<MultiplayerStateSample>(), status, summary, false, 0, 0, ct);
            return new(status, clients, 0, 0, 0, null, null, false, Relative(root), summary);
        }

        var samples = ParseTelemetry(execution.StandardOutput)
            .Concat(ParseTelemetry(execution.StandardError))
            .Where(x => x.Client >= 0 && x.Client < clients)
            .GroupBy(x => (x.Client, x.Tick))
            .Select(x => x.Last())
            .OrderBy(x => x.Tick)
            .ThenBy(x => x.Client)
            .ToArray();
        var impairmentRequested = latency > 0 || jitter > 0 || loss > 0m;
        var assessment = EvaluateTelemetry(
            samples,
            clients,
            Math.Max(1, command.MinimumCommonTicks),
            Math.Max(0, command.MaximumDesyncTicks),
            impairmentRequested);

        await WriteEvidenceAsync(
            root,
            command,
            execution,
            samples,
            assessment.Status,
            assessment.Summary,
            assessment.NetworkImpairmentVerified,
            assessment.CommonTicks,
            assessment.DesyncTicks,
            ct);
        return new(
            assessment.Status,
            clients,
            assessment.Samples,
            assessment.CommonTicks,
            assessment.DesyncTicks,
            assessment.AverageRttMs,
            assessment.AverageObservedPacketLossPercent,
            assessment.NetworkImpairmentVerified,
            Relative(root),
            assessment.Summary);
    }

    public static IReadOnlyList<MultiplayerStateSample> ParseTelemetry(string text)
    {
        var result = new List<MultiplayerStateSample>();
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains(TelemetryPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var values = ParseKeyValues(line);
            if (!TryInt(values, "client", out var client)
                || client < 0
                || !TryLong(values, "tick", out var tick)
                || tick < 0
                || !values.TryGetValue("hash", out var hash)
                || string.IsNullOrWhiteSpace(hash)
                || hash.Length > 512)
                continue;

            var rtt = NullableDecimal(values, "rtt_ms");
            var observedLoss = NullableDecimal(values, "packet_loss_percent");
            if (rtt < 0 || observedLoss < 0 || observedLoss > 100m)
                continue;
            var impairment = values.TryGetValue("network_impairment_applied", out var applied)
                             && (applied == "1" || bool.TryParse(applied, out var parsed) && parsed);
            result.Add(new(client, tick, hash.Trim(), rtt, observedLoss, impairment));
        }
        return result;
    }

    public static MultiplayerTelemetryAssessment EvaluateTelemetry(
        IReadOnlyList<MultiplayerStateSample> input,
        int expectedClients,
        int minimumCommonTicks,
        int maximumDesyncTicks,
        bool impairmentRequested)
    {
        var clients = Math.Clamp(expectedClients, 2, 32);
        var samples = input
            .Where(x => x.Client >= 0 && x.Client < clients && x.Tick >= 0 && !string.IsNullOrWhiteSpace(x.StateHash))
            .GroupBy(x => (x.Client, x.Tick))
            .Select(x => x.Last())
            .ToArray();
        if (samples.Length == 0)
        {
            return new(
                "InfrastructureFailure", 0, 0, 0, null, null, false,
                "Harness completed without valid DIP_MP_STATE telemetry; multiplayer correctness is NOT_EVALUATED.");
        }

        var presentClients = samples.Select(x => x.Client).Distinct().OrderBy(x => x).ToArray();
        if (presentClients.Length != clients)
        {
            return new(
                "InfrastructureFailure",
                samples.Length,
                0,
                0,
                Average(samples.Select(x => x.RttMs)),
                Average(samples.Select(x => x.ObservedPacketLossPercent)),
                false,
                $"Expected telemetry from {clients} clients but observed {presentClients.Length}.");
        }

        var impairmentVerified = !impairmentRequested
                                 || Enumerable.Range(0, clients).All(client =>
                                     samples.Where(x => x.Client == client).Any(x => x.NetworkImpairmentApplied));
        if (!impairmentVerified)
        {
            return new(
                "InfrastructureFailure",
                samples.Length,
                0,
                0,
                Average(samples.Select(x => x.RttMs)),
                Average(samples.Select(x => x.ObservedPacketLossPercent)),
                false,
                "Network impairment was requested but one or more clients did not attest that latency/jitter/loss injection was applied.");
        }

        HashSet<long>? common = null;
        for (var client = 0; client < clients; client++)
        {
            var ticks = samples.Where(x => x.Client == client).Select(x => x.Tick).ToHashSet();
            if (common is null)
                common = ticks;
            else
                common.IntersectWith(ticks);
        }
        common ??= new HashSet<long>();
        var commonTicks = common.OrderBy(x => x).ToArray();
        var requiredTicks = Math.Max(1, minimumCommonTicks);
        if (commonTicks.Length < requiredTicks)
        {
            return new(
                "InfrastructureFailure",
                samples.Length,
                commonTicks.Length,
                0,
                Average(samples.Select(x => x.RttMs)),
                Average(samples.Select(x => x.ObservedPacketLossPercent)),
                impairmentVerified,
                $"Only {commonTicks.Length} common simulation tick(s) were observed; at least {requiredTicks} are required.");
        }

        var byTick = samples.ToLookup(x => x.Tick);
        var desyncTicks = commonTicks.Count(tick =>
        {
            var atTick = byTick[tick].ToArray();
            if (atTick.Length != clients)
                return true;
            return atTick.Select(x => x.StateHash).Distinct(StringComparer.Ordinal).Count() != 1;
        });
        var failed = desyncTicks > Math.Max(0, maximumDesyncTicks);
        var status = failed ? "Failed" : "Passed";
        var avgRtt = Average(samples.Select(x => x.RttMs));
        var avgLoss = Average(samples.Select(x => x.ObservedPacketLossPercent));
        var summary =
            $"{clients} clients, {commonTicks.Length} common tick(s), {desyncTicks} desync tick(s), "
            + $"RTT={(avgRtt?.ToString("0.##", CultureInfo.InvariantCulture) ?? "n/a")}ms, "
            + $"observed loss={(avgLoss?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a")}% .";
        return new(status, samples.Length, commonTicks.Length, desyncTicks, avgRtt, avgLoss, impairmentVerified, summary);
    }

    private static Dictionary<string, string> ParseKeyValues(string line)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in line.Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split('=', 2);
            if (parts.Length == 2)
                values[parts[0].Trim()] = parts[1].Trim();
        }
        return values;
    }

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var actual = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return actual.Length == 0 ? null : decimal.Round(actual.Average(), 3);
    }

    private static bool TryInt(IReadOnlyDictionary<string, string> values, string key, out int result)
        => values.TryGetValue(key, out var text)
           && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryLong(IReadOnlyDictionary<string, string> values, string key, out long result)
        => values.TryGetValue(key, out var text)
           && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static decimal? NullableDecimal(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var text)
           && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string ResolveExecutable(string workspace, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new ArgumentException("Multiplayer QA harness executable is required.", nameof(executable));
        if (Path.IsPathRooted(executable))
            return executable;
        var local = Path.GetFullPath(Path.Combine(workspace, executable));
        return File.Exists(local) ? local : executable;
    }

    private static async Task WriteEvidenceAsync(
        string root,
        MultiplayerQaCommand command,
        ProcessExecutionResult execution,
        IReadOnlyList<MultiplayerStateSample> samples,
        string status,
        string summary,
        bool impairmentVerified,
        int commonTicks,
        int desyncTicks,
        CancellationToken ct)
    {
        var payload = new
        {
            schema = TelemetrySchema,
            telemetryContract = new
            {
                prefix = TelemetryPrefix,
                format = "DIP_MP_STATE client=<n> tick=<n> hash=<stable-state-hash> [rtt_ms=<ms>] [packet_loss_percent=<0..100>] network_impairment_applied=<0|1>"
            },
            status,
            summary,
            requested = new
            {
                command.Clients,
                command.DurationSeconds,
                command.LatencyMs,
                command.JitterMs,
                command.PacketLossPercent,
                command.MinimumCommonTicks,
                command.MaximumDesyncTicks
            },
            impairmentVerified,
            commonTicks,
            desyncTicks,
            execution = new
            {
                execution.ExitCode,
                execution.TimedOut,
                execution.DurationMs,
                execution.Sandbox
            },
            samples
        };
        await File.WriteAllTextAsync(
            Path.Combine(root, "multiplayer-qa.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            ct);
    }

    private static string Relative(string full)
        => Path.GetRelativePath(Path.GetFullPath("."), full).Replace('\\', '/');
}
