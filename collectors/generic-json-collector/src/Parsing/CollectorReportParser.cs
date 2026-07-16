using System.Text.Json;
using GenericJsonCollector.Models;

namespace GenericJsonCollector.Parsing;

public sealed class CollectorReportParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CollectorReport> ParseAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CollectorReport>(stream, Options, cancellationToken)
            ?? throw new InvalidDataException("Collector report JSON was empty.");
    }
}
