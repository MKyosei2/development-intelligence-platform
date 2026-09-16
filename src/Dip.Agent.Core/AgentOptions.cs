// Purpose: Implements A ge nt Op ti on s agent behavior.
namespace Dip.Agent.Core;

public sealed record AgentOptions(
    Uri ApiBaseUrl,
    Guid ProjectId,
    string AgentId,
    string RootId,
    string ProjectRoot,
    string StateDatabasePath,
    string? BearerToken,
    TimeSpan ReconcileInterval,
    IReadOnlyList<string> ExcludedDirectoryNames,
    int BatchSize = 100,
    string? PipeName = null,
    string? BridgeToken = null,
    string? MediaSpoolDirectory = null,
    long MaxMediaUploadBytes = 1024L * 1024 * 1024)
{
    public static AgentOptions Validate(AgentOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.ApiBaseUrl.IsAbsoluteUri) throw new ArgumentException("ApiBaseUrl must be absolute.");
        if (value.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId is required.");
        if (string.IsNullOrWhiteSpace(value.AgentId)) throw new ArgumentException("AgentId is required.");
        if (string.IsNullOrWhiteSpace(value.RootId)) throw new ArgumentException("RootId is required.");
        var root = Path.GetFullPath(value.ProjectRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if (value.ReconcileInterval < TimeSpan.FromSeconds(2)) throw new ArgumentException("ReconcileInterval must be at least two seconds.");
        if (value.BatchSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(value.BatchSize));
        if (value.MaxMediaUploadBytes is < 1 or > 2L * 1024 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(value.MaxMediaUploadBytes));

        var state = Path.GetFullPath(value.StateDatabasePath);
        var stateDirectory = Path.GetDirectoryName(state)
            ?? throw new ArgumentException("StateDatabasePath must have a directory.");
        var pipe = string.IsNullOrWhiteSpace(value.PipeName)
            ? "dip-production-" + value.ProjectId.ToString("N")
            : value.PipeName.Trim();
        if (pipe.Length > 200 || pipe.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("PipeName is invalid.");
        var spool = string.IsNullOrWhiteSpace(value.MediaSpoolDirectory)
            ? Path.Combine(stateDirectory, "media-spool")
            : Path.GetFullPath(value.MediaSpoolDirectory);

        return value with
        {
            AgentId = value.AgentId.Trim(),
            RootId = value.RootId.Trim(),
            ProjectRoot = root,
            StateDatabasePath = state,
            PipeName = pipe,
            BridgeToken = string.IsNullOrWhiteSpace(value.BridgeToken) ? null : value.BridgeToken.Trim(),
            MediaSpoolDirectory = spool,
            ExcludedDirectoryNames = value.ExcludedDirectoryNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public static readonly string[] DefaultExcludedDirectories =
    [
        ".git", ".vs", ".idea", ".dip", "bin", "obj", "node_modules", "Library", "Temp"
    ];
}
