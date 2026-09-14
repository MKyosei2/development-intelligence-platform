using System.Text.RegularExpressions;

namespace Dip.Application.Features.Evidence;

public static partial class EvidenceValidator
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    public static void Validate(IngestEvidenceCommand command, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.SourceInstanceId))
            throw new ArgumentException("SourceInstanceId is required.", nameof(command));
        if (command.SourceInstanceId.Trim().Length > 200)
            throw new ArgumentException("SourceInstanceId is too long.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.SourceEventId))
            throw new ArgumentException("SourceEventId is required.", nameof(command));
        if (command.SourceEventId.Trim().Length > 300)
            throw new ArgumentException("SourceEventId is too long.", nameof(command));
        if (command.ObservedAtUtc > now.AddMinutes(5))
            throw new ArgumentException("ObservedAtUtc is too far in the future.", nameof(command));
        if (!string.IsNullOrWhiteSpace(command.ContentHash) &&
            !Sha256Regex().IsMatch(command.ContentHash.Trim()))
            throw new ArgumentException(
                "ContentHash must be a 64-character SHA-256 hexadecimal value.",
                nameof(command));

        if (command.Metadata is { } metadata &&
            metadata.GetRawText().Length > 32000)
            throw new ArgumentException("Metadata JSON is too large.", nameof(command));

        if (command.Payload is { } payload &&
            payload.GetRawText().Length > 16000)
            throw new ArgumentException("Payload JSON is too large.", nameof(command));
    }
}
