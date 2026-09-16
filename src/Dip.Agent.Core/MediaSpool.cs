// Purpose: Implements M ed ia Sp oo l agent behavior.
using System.Security.Cryptography;

namespace Dip.Agent.Core;

public static class MediaSpool
{
    public static async Task<QueuedMediaUpload?> CreateAsync(
        AgentOptions options,
        ToolEventObservation item,
        CancellationToken ct)
    {
        if (!item.MediaKind.HasValue || string.IsNullOrWhiteSpace(item.MediaRelativePath))
            return null;

        var relative = NormalizeRelativePath(item.MediaRelativePath);
        var root = Path.GetFullPath(options.ProjectRoot);
        var source = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!source.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Media path escaped the configured project root.");
        if (!File.Exists(source))
            return null;

        var info = new FileInfo(source);
        if (info.Length > options.MaxMediaUploadBytes)
            throw new InvalidOperationException($"Media file exceeds configured upload limit ({options.MaxMediaUploadBytes} bytes).");

        Directory.CreateDirectory(options.MediaSpoolDirectory);
        var extension = Path.GetExtension(source);
        var spoolName = Sanitize(item.EventId) + "-" + Guid.NewGuid().ToString("N") + extension;
        var spool = Path.Combine(options.MediaSpoolDirectory, spoolName);
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 128, true))
        await using (var output = new FileStream(spool, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true))
            await input.CopyToAsync(output, ct);

        string sha;
        await using (var stream = new FileStream(spool, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true))
        using (var hash = SHA256.Create())
            sha = Convert.ToHexString(await hash.ComputeHashAsync(stream, ct)).ToLowerInvariant();

        var contentType = string.IsNullOrWhiteSpace(item.MediaContentType)
            ? ContentType(relative)
            : item.MediaContentType.Trim().ToLowerInvariant();

        return new QueuedMediaUpload(
            "media:" + item.EventId,
            item.EventId,
            item.Source,
            item.MediaKind.Value,
            relative,
            spool,
            contentType,
            sha,
            new FileInfo(spool).Length,
            DateTimeOffset.UtcNow,
            0);
    }

    private static string NormalizeRelativePath(string value)
    {
        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Length is < 1 or > 1000 || Path.IsPathRooted(normalized) || normalized.Split('/').Any(x => x is "." or ".."))
            throw new ArgumentException("MediaRelativePath must be a normalized project-relative path.", nameof(value));
        return normalized;
    }

    private static string Sanitize(string value)
        => new(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').Take(100).ToArray());

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        _ => "application/octet-stream"
    };
}
