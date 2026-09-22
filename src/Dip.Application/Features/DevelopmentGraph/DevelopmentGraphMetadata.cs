// Purpose: Centralizes Development Graph metadata normalization, validation, merging, and component inference.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

internal static class DevelopmentGraphMetadata
{
    internal static string? InferComponent(string path)
        {
            var parts = Normalize(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return null;
    
            if (parts[0].Equals("src", StringComparison.OrdinalIgnoreCase)
                || parts[0].Equals("tests", StringComparison.OrdinalIgnoreCase)
                || parts[0].Equals("collectors", StringComparison.OrdinalIgnoreCase)
                || parts[0].Equals("services", StringComparison.OrdinalIgnoreCase))
            {
                return $"{parts[0]}/{parts[1]}";
            }
    
            return parts.Length >= 3 && parts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase)
                ? $"Assets/{parts[1]}"
                : parts[0];
        }
    
    internal static string Normalize(string path) =>
            path.Replace('\\', '/').Trim('/');
    
    internal static string Json(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "{}" : value;
            try
            {
                using var _ = JsonDocument.Parse(normalized);
                return normalized;
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("MetadataJson must contain valid JSON.", nameof(value), ex);
            }
        }
    
    internal static string MergeMetadata(
            string json,
            IReadOnlyDictionary<string, string?> fields)
        {
            JsonNode? parsed;
            try
            {
                parsed = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("MetadataJson must contain valid JSON.", nameof(json), ex);
            }
    
            var obj = parsed as JsonObject;
            if (obj is null)
            {
                obj = new JsonObject();
                if (parsed is not null)
                    obj["payload"] = parsed.DeepClone();
            }
    
            foreach (var pair in fields)
            {
                if (pair.Value is null)
                    obj.Remove(pair.Key);
                else
                    obj[pair.Key] = pair.Value;
            }
    
            return obj.ToJsonString();
        }
    
    internal static string Identifier(string value, int max, string name)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length is < 1 || normalized.Length > max)
            {
                throw new ArgumentException($"{name} must be 1-{max} characters.", name);
            }
    
            if (normalized.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or ':' or '/')))
            {
                throw new ArgumentException($"{name} contains unsupported characters.", name);
            }
    
            return normalized;
        }
    
    internal static string? Optional(string? value, int max, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
    
            var normalized = value.Trim();
            if (normalized.Length > max)
                throw new ArgumentException($"{name} must be at most {max} characters.", name);
    
            return normalized;
        }
    
    internal static string? MetadataString(string json, string name, string context)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return null;
                if (!document.RootElement.TryGetProperty(name, out var value)
                    || value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }
                return value.GetString();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Malformed metadata was encountered while reading {context}.",
                    ex);
            }
        }
}
