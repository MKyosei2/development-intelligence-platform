// Purpose: Defines D ev el op me nt Gr ap hN od e domain behavior and invariants for p ro je ct s.
using System.Text.Json.Nodes;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>
/// Durable, provider-neutral node in the development knowledge graph. The graph
/// connects source changes, QA, assets, features, milestones and releases without
/// assigning productivity scores to people.
/// </summary>
public sealed class DevelopmentGraphNode
{
    private static readonly string[] UniversalIdentityKeys = ["domainId", "kind", "schemaVersion", "sourceObjectId"];

    private DevelopmentGraphNode() { }

    public DevelopmentGraphNode(Guid id, Guid projectId, DevelopmentGraphNodeType type, string key, string name, string status, string? provider, string? externalReference, string metadataJson, DateTimeOffset observedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Id = id == Guid.Empty ? Guid.NewGuid() : id; ProjectId = projectId; Type = type;
        Key = Required(key, 300, nameof(key)); Name = Required(name, 300, nameof(name)); Status = Required(status, 80, nameof(status));
        Provider = Optional(provider, 80, nameof(provider)); ExternalReference = Optional(externalReference, 2000, nameof(externalReference));
        MetadataJson = Required(metadataJson, 64000, nameof(metadataJson)); FirstObservedAtUtc = observedAtUtc; LastObservedAtUtc = observedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public DevelopmentGraphNodeType Type { get; private set; }
    public string Key { get; private set; } = string.Empty; public string Name { get; private set; } = string.Empty; public string Status { get; private set; } = string.Empty;
    public string? Provider { get; private set; }
    public string? ExternalReference { get; private set; }
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset FirstObservedAtUtc { get; private set; }
    public DateTimeOffset LastObservedAtUtc { get; private set; }

    public void Observe(string name, string status, string? provider, string? externalReference, string metadataJson, DateTimeOffset observedAtUtc)
    {
        Name = Required(name, 300, nameof(name)); Status = Required(status, 80, nameof(status)); Provider = Optional(provider, 80, nameof(provider));
        ExternalReference = Optional(externalReference, 2000, nameof(externalReference));
        MetadataJson = PreserveUniversalIdentity(MetadataJson, Required(metadataJson, 64000, nameof(metadataJson)));
        if (observedAtUtc > LastObservedAtUtc) LastObservedAtUtc = observedAtUtc;
    }

    /// <summary>
    /// Returns one string metadata value without making callers depend on a
    /// provider-specific schema. Invalid/non-object legacy payloads simply
    /// return null rather than breaking reads.
    /// </summary>
    public string? MetadataValue(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var node = JsonNode.Parse(MetadataJson) as JsonObject;
            if (node is null) return null;
            foreach (var pair in node)
                if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return pair.Value?.GetValue<string>();
        }
        catch {
            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
        }
        return null;
    }

    private static string PreserveUniversalIdentity(string existing, string incoming)
    {
        try
        {
            var oldObject = JsonNode.Parse(existing) as JsonObject;
            var newObject = JsonNode.Parse(incoming) as JsonObject;
            if (oldObject is null || newObject is null) return incoming;
            foreach (var key in UniversalIdentityKeys)
                if (!newObject.ContainsKey(key) && oldObject[key] is { } value)
                    newObject[key] = value.DeepClone();
            return newObject.ToJsonString();
        }
        catch
        {
            // Preserve legacy behavior for old payloads that were stored before
            // metadata became structured JSON.
            return incoming;
        }
    }

    private static string Required(string? value, int max, string name)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return v;
    }
    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Length > max) throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return v;
    }
}
