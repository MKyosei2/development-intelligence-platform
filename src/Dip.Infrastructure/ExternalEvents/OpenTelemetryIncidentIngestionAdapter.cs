// Purpose: Provides infrastructure behavior for this application boundary.
using System.Text.Json;
using Dip.Application.Features.ExternalEvents;

namespace Dip.Infrastructure.ExternalEvents;

public sealed class OpenTelemetryIncidentIngestionAdapter : IExternalIncidentIngestionAdapter
{
    public string Provider => "opentelemetry";
    public bool CanHandle(string provider) => provider.Equals("otel", StringComparison.OrdinalIgnoreCase) || provider.Equals("opentelemetry", StringComparison.OrdinalIgnoreCase);
    public ExternalIncidentEnvelope Parse(JsonElement p)
    {
        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); Flatten(p, "", flat, 0);
        string? Get(params string[] keys) => keys.Select(k => flat.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        var type = Get("exception.type", "attributes.exception.type", "body.exception.type");
        var message = Get("exception.message", "attributes.exception.message", "body", "message") ?? "OpenTelemetry runtime event";
        var stack = Get("exception.stacktrace", "attributes.exception.stacktrace");
        var file = Get("code.filepath", "attributes.code.filepath");
        int? line = int.TryParse(Get("code.lineno", "attributes.code.lineno"), out var n) ? n : null;
        var title = type is null ? "OpenTelemetry runtime event" : type;
        var route = Get("http.route", "attributes.http.route", "scene", "attributes.scene");
        var id = Get("trace_id", "traceId", "span_id", "spanId") ?? Guid.NewGuid().ToString("N");
        var tags = flat.Where(x => x.Key.StartsWith("attributes.", StringComparison.OrdinalIgnoreCase)).Take(100).ToDictionary(x => x.Key[11..], x => x.Value, StringComparer.OrdinalIgnoreCase);
        return new(id, title, message, type, stack, file, line, route, tags, DateTimeOffset.UtcNow);
    }
    private static void Flatten(JsonElement e, string prefix, Dictionary<string, string> into, int depth)
    {
        if (depth > 6) return;
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject()) Flatten(p.Value, string.IsNullOrEmpty(prefix) ? p.Name : prefix + "." + p.Name, into, depth + 1);
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var x in e.EnumerateArray().Take(20)) Flatten(x, prefix + "." + i++, into, depth + 1);
        }
        else if (!string.IsNullOrEmpty(prefix)) into[prefix] = e.ToString();
    }
}
