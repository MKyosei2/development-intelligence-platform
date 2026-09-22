// Purpose: Defines a focused responsibility within the development intelligence platform.
using System.Text.Json;
using System.Text.Json.Serialization;
using GenericJsonCollector.Models;

namespace GenericJsonCollector.Parsing;

public sealed class CollectorReportParser
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow
        };

    static CollectorReportParser()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<CollectorReport> ParseAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Report path is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                $"Collector report was not found: {fullPath}",
                fullPath);

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            useAsync: true);

        if (stream.Length == 0)
            throw new InvalidDataException("Collector report JSON was empty.");

        if (stream.Length > 10 * 1024 * 1024)
            throw new InvalidDataException(
                "Collector report exceeds the 10 MiB limit.");

        try
        {
            return await JsonSerializer.DeserializeAsync<CollectorReport>(
                       stream,
                       Options,
                       cancellationToken)
                   ?? throw new InvalidDataException(
                       "Collector report JSON resolved to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Collector report JSON is invalid at line " +
                $"{exception.LineNumber}, byte {exception.BytePositionInLine}: " +
                exception.Message,
                exception);
        }
    }
}
