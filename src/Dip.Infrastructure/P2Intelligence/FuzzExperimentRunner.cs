// Purpose: Runs deterministic coverage-guided, game-state, and protocol-aware fuzz experiments with failure minimization.
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

internal sealed class FuzzExperimentRunner(ProcessExecutor executor, IQaWorkspaceResolver workspaceResolver)
    : ExperimentExecutionSupport(executor, workspaceResolver)
{
    public async Task<FuzzResult> RunFuzzAsync(FuzzCommand command, CancellationToken ct)
    {
        var workspace = workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable = ResolveTargetExecutable(workspace, command.Target.Executable);
        var root = ArtifactRoot(command.Target.ProjectId, "fuzz");
        var corpusDirectory = Path.Combine(root, "corpus");
        var seedDirectory = Path.Combine(corpusDirectory, "seed");
        var interestingDirectory = Path.Combine(corpusDirectory, "interesting");
        var crashDirectory = Path.Combine(corpusDirectory, "crash");
        var timeoutDirectory = Path.Combine(corpusDirectory, "timeout");
        Directory.CreateDirectory(seedDirectory);
        Directory.CreateDirectory(interestingDirectory);
        Directory.CreateDirectory(crashDirectory);
        Directory.CreateDirectory(timeoutDirectory);

        var configuredArguments = command.Target.Arguments ?? Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(command.ArgumentPlaceholder)
            || !configuredArguments.Any(a => a.Contains(command.ArgumentPlaceholder, StringComparison.Ordinal)))
        {
            var errorPath = Path.Combine(root, "fuzz-manifest.json");
            await File.WriteAllTextAsync(errorPath, JsonSerializer.Serialize(new
            {
                status = "InfrastructureFailure",
                reason = "At least one configured target argument must contain the fuzz input placeholder.",
                argumentPlaceholder = command.ArgumentPlaceholder
            }, new JsonSerializerOptions { WriteIndented = true }), ct);
            return new("InfrastructureFailure", 0, 0, 0, 0, Array.Empty<FuzzCaseResult>(), RelativeArtifact(root), Array.Empty<FuzzFailureArtifact>());
        }

        var mode = DetectMode(command.Target.SubjectKey, configuredArguments);
        var random = new Random(command.Seed);
        var results = new List<FuzzCaseResult>();
        var failures = new List<FuzzFailureArtifact>();
        var seenFailureFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCoverage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var corpus = BuildInitialCorpus(mode, Math.Clamp(command.MaxInputBytes, 1, 1024 * 1024));
        var infrastructureFailure = false;
        var cases = Math.Clamp(command.Cases, 1, 10000);
        var max = Math.Clamp(command.MaxInputBytes, 1, 1024 * 1024);
        var transient = Path.Combine(root, "current-input.bin");

        for (var i = 0; i < Math.Min(corpus.Count, 16); i++)
            await File.WriteAllBytesAsync(Path.Combine(seedDirectory, $"seed-{i:D2}.bin"), corpus[i], ct);

        try
        {
            for (var i = 0; i < cases; i++)
            {
                ct.ThrowIfCancellationRequested();
                var bytes = BuildAdaptiveInput(i, max, random, corpus, mode);
                await File.WriteAllBytesAsync(transient, bytes, ct);
                var arguments = configuredArguments
                    .Select(a => a.Replace(command.ArgumentPlaceholder, transient, StringComparison.Ordinal))
                    .ToArray();
                var directory = Path.Combine(root, "runs", $"case-{i:D5}");
                Directory.CreateDirectory(directory);
                var result = await executor.RunAsync(
                    executable,
                    arguments,
                    workspace,
                    new Dictionary<string, string>
                    {
                        ["DIP_FUZZ"] = "1",
                        ["DIP_FUZZ_MODE"] = mode,
                        ["DIP_FUZZ_SEED"] = command.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["DIP_FUZZ_CASE"] = i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    },
                    TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds, 1, 600)),
                    Path.Combine(directory, "stdout.log"),
                    Path.Combine(directory, "stderr.log"),
                    ct);

                if (!result.TimedOut && result.ExitCode is null)
                {
                    infrastructureFailure = true;
                    results.Add(new(i, bytes.Length, null, false, false, Fingerprint(result.StandardError), null));
                    break;
                }

                var crashed = !result.TimedOut && result.ExitCode.HasValue && result.ExitCode.Value != 0;
                var failureFingerprint = Fingerprint(result.StandardOutput + "\n" + result.StandardError);
                var coverageFingerprint = ExtractCoverageFingerprint(result.StandardOutput, result.StandardError)
                                          ?? "output:" + failureFingerprint;
                var failure = crashed || result.TimedOut;
                string? inputReference = null;

                if (failure)
                {
                    var kind = crashed ? "crash" : "timeout";
                    var destinationDirectory = crashed ? crashDirectory : timeoutDirectory;
                    var destination = Path.Combine(destinationDirectory, $"{failureFingerprint}-{i:D5}.bin");
                    await File.WriteAllBytesAsync(destination, bytes, ct);
                    inputReference = RelativeArtifact(destination);
                    if (seenFailureFingerprints.Add(failureFingerprint))
                    {
                        string? minimizedReference = null;
                        int? minimizedBytes = null;
                        if (command.MinimizeFailures && bytes.Length > 1)
                        {
                            var minimized = await MinimizeFailureAsync(executable, workspace, command, bytes, failureFingerprint, crashed, ct);
                            if (minimized.Length < bytes.Length)
                            {
                                var minimizedPath = Path.Combine(destinationDirectory, $"{failureFingerprint}-{i:D5}.min.bin");
                                await File.WriteAllBytesAsync(minimizedPath, minimized, ct);
                                minimizedReference = RelativeArtifact(minimizedPath);
                                minimizedBytes = minimized.Length;
                            }
                        }
                        failures.Add(new(i, kind, failureFingerprint, inputReference, minimizedReference, bytes.Length, minimizedBytes));
                    }
                }
                else if (seenCoverage.Add(coverageFingerprint))
                {
                    var interesting = Path.Combine(interestingDirectory, $"{SafeCoverageName(coverageFingerprint)}-{i:D5}.bin");
                    await File.WriteAllBytesAsync(interesting, bytes, ct);
                    inputReference = RelativeArtifact(interesting);
                    if (corpus.Count < 512)
                        corpus.Add(bytes.ToArray());
                }

                results.Add(new(i, bytes.Length, result.ExitCode, result.TimedOut, crashed, failureFingerprint, inputReference));
            }
        }
        finally
        {
            try
            {
                if (File.Exists(transient)) File.Delete(transient);
            }
            catch (IOException ex)
            {
                await File.AppendAllTextAsync(Path.Combine(root, "fuzz-cleanup-warning.log"), ex.Message + Environment.NewLine, CancellationToken.None);
            }
            catch (UnauthorizedAccessException ex)
            {
                await File.AppendAllTextAsync(Path.Combine(root, "fuzz-cleanup-warning.log"), ex.Message + Environment.NewLine, CancellationToken.None);
            }
        }

        var crashes = results.Count(x => x.Crashed);
        var timeouts = results.Count(x => x.TimedOut);
        var unique = results
            .Where(x => x.Crashed || x.TimedOut)
            .Select(x => x.Fingerprint)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var status = infrastructureFailure
            ? "InfrastructureFailure"
            : crashes > 0
                ? "Failed"
                : timeouts > 0
                    ? "Warning"
                    : "Passed";
        var manifest = JsonSerializer.Serialize(new
        {
            schema = "dip.fuzz.v2",
            mode,
            coverageSignal = "DIP_COVERAGE hash=<stable-id>; normalized output fingerprint is used only as fallback",
            command.Seed,
            casesRequested = cases,
            casesExecuted = results.Count,
            maxInputBytes = max,
            crashes,
            timeouts,
            uniqueFailureFingerprints = unique,
            uniqueCoverageFingerprints = seenCoverage.Count,
            adaptiveCorpusSize = corpus.Count,
            seedCorpus = Directory.EnumerateFiles(seedDirectory).Select(RelativeArtifact).ToArray(),
            failures
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(root, "fuzz-manifest.json"), manifest, ct);
        return new(status, results.Count, crashes, timeouts, unique, results, RelativeArtifact(root), failures);
    }

    private static string DetectMode(string subjectKey, IReadOnlyList<string> arguments)
    {
        var text = (subjectKey + " " + string.Join(' ', arguments)).ToLowerInvariant();
        if (new[] { "protocol", "packet", "network", "socket", "http", "api", "rpc", "serialize" }.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)))
            return "protocol";
        if (new[] { "game", "scene", "save", "replay", "state", "level", "entity", "player" }.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)))
            return "game-state";
        return "coverage-guided";
    }

    private static List<byte[]> BuildInitialCorpus(string mode, int max)
    {
        var seeds = new List<byte[]>
        {
            Array.Empty<byte>(),
            new byte[] { 0 },
            new byte[] { 0xff },
            Encoding.UTF8.GetBytes("0"),
            Encoding.UTF8.GetBytes("null"),
            Encoding.UTF8.GetBytes("{}"),
            Encoding.UTF8.GetBytes("[]")
        };
        if (mode == "protocol")
        {
            seeds.Add(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n"));
            seeds.Add(Encoding.UTF8.GetBytes("{\"type\":\"ping\",\"id\":0,\"payload\":{}}"));
            seeds.Add(new byte[] { 0, 0, 0, 0 });
            seeds.Add(new byte[] { 0xff, 0xff, 0xff, 0xff });
        }
        else if (mode == "game-state")
        {
            seeds.Add(Encoding.UTF8.GetBytes("{\"frame\":0,\"seed\":0,\"player\":{\"x\":0,\"y\":0,\"z\":0}}"));
            seeds.Add(Encoding.UTF8.GetBytes("{\"frame\":2147483647,\"seed\":-1,\"entities\":[]}"));
            seeds.Add(Encoding.UTF8.GetBytes("{\"scene\":\"\",\"checkpoint\":-1,\"inventory\":[]}"));
        }
        return seeds.Select(x => x.Length <= max ? x : x[..max]).ToList();
    }

    private static byte[] BuildAdaptiveInput(int index, int max, Random random, IReadOnlyList<byte[]> corpus, string mode)
    {
        if (index < corpus.Count)
            return corpus[index].ToArray();
        if (index < 24)
            return BuildFuzzInput(index, max, random);

        var seed = corpus.Count == 0 ? BuildFuzzInput(index, max, random) : corpus[random.Next(corpus.Count)].ToArray();
        if (seed.Length == 0) seed = new byte[Math.Min(max, 1)];
        var output = seed.ToList();
        var operations = random.Next(1, 5);
        for (var op = 0; op < operations; op++)
        {
            var choice = random.Next(0, mode == "protocol" ? 7 : 6);
            switch (choice)
            {
                case 0 when output.Count > 0:
                    output[random.Next(output.Count)] ^= (byte)(1 << random.Next(8));
                    break;
                case 1 when output.Count > 0:
                    output[random.Next(output.Count)] = random.Next(2) == 0 ? (byte)0 : (byte)0xff;
                    break;
                case 2 when output.Count < max:
                    output.Insert(random.Next(output.Count + 1), (byte)random.Next(256));
                    break;
                case 3 when output.Count > 1:
                    output.RemoveAt(random.Next(output.Count));
                    break;
                case 4 when output.Count > 0 && output.Count < max:
                {
                    var start = random.Next(output.Count);
                    var length = Math.Min(random.Next(1, Math.Min(32, output.Count - start) + 1), max - output.Count);
                    if (length > 0) output.InsertRange(random.Next(output.Count + 1), output.Skip(start).Take(length));
                    break;
                }
                case 5 when output.Count > 0:
                {
                    var indexToChange = random.Next(output.Count);
                    output[indexToChange] = random.Next(4) switch { 0 => 0x7f, 1 => 0x80, 2 => 0xfe, _ => 0x01 };
                    break;
                }
                case 6:
                {
                    var token = random.Next(3) switch
                    {
                        0 => Encoding.ASCII.GetBytes("\r\n\r\n"),
                        1 => Encoding.UTF8.GetBytes("{\"length\":4294967295}"),
                        _ => new byte[] { 0, 0, 0, 1 }
                    };
                    if (output.Count + token.Length <= max)
                        output.InsertRange(random.Next(output.Count + 1), token);
                    break;
                }
            }
        }
        if (mode == "game-state")
            MutateGameStateText(output, max, random);
        return output.Take(max).ToArray();
    }

    private static void MutateGameStateText(List<byte> bytes, int max, Random random)
    {
        if (bytes.Count == 0 || bytes.Count >= max) return;
        var text = Encoding.UTF8.GetString(bytes.ToArray());
        if (!text.Contains('{') || !text.Contains('}')) return;
        var injection = random.Next(3) switch
        {
            0 => ",\"nan\":1e999",
            1 => ",\"count\":-2147483648",
            _ => ",\"state\":null"
        };
        var last = text.LastIndexOf('}');
        if (last <= 0) return;
        var mutated = text.Insert(last, injection);
        var encoded = Encoding.UTF8.GetBytes(mutated);
        if (encoded.Length > max) return;
        bytes.Clear();
        bytes.AddRange(encoded);
    }

    private static string? ExtractCoverageFingerprint(string stdout, string stderr)
    {
        foreach (var line in (stdout + "\n" + stderr).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("DIP_COVERAGE", StringComparison.OrdinalIgnoreCase)) continue;
            var match = Regex.Match(line, @"(?:hash|id|edge|coverage)\s*=\s*([A-Za-z0-9_.:-]{1,200})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success) return "coverage:" + match.Groups[1].Value;
            return "coverage:" + Fingerprint(line);
        }
        return null;
    }

    private static string SafeCoverageName(string value)
    {
        var text = string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        return string.IsNullOrWhiteSpace(text) ? "coverage" : text[..Math.Min(80, text.Length)];
    }
}
