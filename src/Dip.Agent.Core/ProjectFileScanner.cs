// Purpose: Implements P ro je ct Fi le Sc an ne r agent behavior.
using System.Security.Cryptography;
using System.Text;

namespace Dip.Agent.Core;

public sealed class ProjectFileScanner
{
    private readonly AgentOptions _options;

    public ProjectFileScanner(AgentOptions options)
        => _options = AgentOptions.Validate(options);

    public async Task<ScanResult> ScanAsync(
        IReadOnlyDictionary<string, FileSnapshot> previous,
        CancellationToken ct)
    {
        var current = new Dictionary<string, FileSnapshot>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var file in EnumerateProjectFiles(warnings))
        {
            ct.ThrowIfCancellationRequested();
            var relative = Normalize(Path.GetRelativePath(_options.ProjectRoot, file));
            if (IsExcluded(relative)) continue;

            try
            {
                var info = new FileInfo(file);
                var modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                string sha;
                if (previous.TryGetValue(relative, out var prior)
                    && prior.SizeBytes == info.Length
                    && prior.LastWriteUtc == modified)
                {
                    sha = prior.Sha256;
                }
                else
                {
                    sha = await Sha256Async(file, ct);
                }
                current[relative] = new FileSnapshot(relative, info.Length, modified, sha);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{relative}: {exception.Message}");
            }
        }

        var observations = BuildObservations(previous, current);
        return new ScanResult(observations, current, warnings);
    }

    private IEnumerable<string> EnumerateProjectFiles(List<string> warnings)
    {
        var pending = new Stack<string>();
        pending.Push(_options.ProjectRoot);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(directory);
                directories = Directory.GetDirectories(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{directory}: {exception.Message}");
                continue;
            }

            foreach (var file in files)
                yield return file;

            foreach (var child in directories)
            {
                var name = Path.GetFileName(child);
                if (_options.ExcludedDirectoryNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;
                pending.Push(child);
            }
        }
    }

    private IReadOnlyList<LocalFileObservation> BuildObservations(
        IReadOnlyDictionary<string, FileSnapshot> previous,
        IReadOnlyDictionary<string, FileSnapshot> current)
    {
        var now = DateTimeOffset.UtcNow;
        var created = current.Values.Where(x => !previous.ContainsKey(x.RelativePath)).ToList();
        var deleted = previous.Values.Where(x => !current.ContainsKey(x.RelativePath)).ToList();
        var output = new List<LocalFileObservation>();

        // Conservative rename detection: only pair when a content hash occurs once
        // on both sides. Ambiguous duplicates remain delete + create.
        foreach (var oldItem in deleted.ToArray())
        {
            var matches = created.Where(x =>
                x.Sha256.Equals(oldItem.Sha256, StringComparison.OrdinalIgnoreCase)
                && x.SizeBytes == oldItem.SizeBytes).ToArray();
            if (matches.Length != 1) continue;
            var newItem = matches[0];
            var reverseCount = deleted.Count(x =>
                x.Sha256.Equals(newItem.Sha256, StringComparison.OrdinalIgnoreCase)
                && x.SizeBytes == newItem.SizeBytes);
            if (reverseCount != 1) continue;
            output.Add(Observation(FileObservationKind.Renamed, newItem, oldItem.RelativePath, newItem.LastWriteUtc));
            created.Remove(newItem);
            deleted.Remove(oldItem);
        }

        foreach (var item in created)
            output.Add(Observation(FileObservationKind.Created, item, null, item.LastWriteUtc));

        foreach (var item in current.Values)
        {
            if (!previous.TryGetValue(item.RelativePath, out var old)) continue;
            if (item.Sha256.Equals(old.Sha256, StringComparison.OrdinalIgnoreCase)
                && item.SizeBytes == old.SizeBytes) continue;
            output.Add(Observation(FileObservationKind.Modified, item, null, item.LastWriteUtc));
        }

        foreach (var item in deleted)
        {
            var eventId = EventId(FileObservationKind.Deleted, item.RelativePath, null, item.Sha256, now);
            output.Add(new LocalFileObservation(
                eventId, FileObservationKind.Deleted, item.RelativePath, null,
                Path.GetExtension(item.RelativePath).ToLowerInvariant(), null, null, null, now));
        }

        return output.OrderBy(x => x.ObservedAtUtc).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private LocalFileObservation Observation(
        FileObservationKind kind,
        FileSnapshot item,
        string? previousPath,
        DateTimeOffset observedAt)
        => new(
            EventId(kind, item.RelativePath, previousPath, item.Sha256, observedAt),
            kind,
            item.RelativePath,
            previousPath,
            Path.GetExtension(item.RelativePath).ToLowerInvariant(),
            item.SizeBytes,
            item.Sha256,
            item.LastWriteUtc,
            observedAt);

    private string EventId(FileObservationKind kind, string path, string? previous, string? sha, DateTimeOffset observed)
    {
        var raw = $"{_options.AgentId}|{_options.RootId}|{kind}|{path}|{previous}|{sha}|{observed:O}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return "local-file:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    private bool IsExcluded(string relative)
    {
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => _options.ExcludedDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 128];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
