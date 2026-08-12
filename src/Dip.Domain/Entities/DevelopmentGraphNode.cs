using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>
/// Durable, provider-neutral node in the development knowledge graph. The graph
/// connects source changes, QA, assets, features, milestones and releases without
/// assigning productivity scores to people.
/// </summary>
public sealed class DevelopmentGraphNode
{
    private DevelopmentGraphNode() { }

    public DevelopmentGraphNode(Guid id,Guid projectId,DevelopmentGraphNodeType type,string key,string name,string status,string? provider,string? externalReference,string metadataJson,DateTimeOffset observedAtUtc)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        if(!Enum.IsDefined(type))throw new ArgumentOutOfRangeException(nameof(type));
        Id=id==Guid.Empty?Guid.NewGuid():id;ProjectId=projectId;Type=type;
        Key=Required(key,300,nameof(key));Name=Required(name,300,nameof(name));Status=Required(status,80,nameof(status));
        Provider=Optional(provider,80,nameof(provider));ExternalReference=Optional(externalReference,2000,nameof(externalReference));
        MetadataJson=Required(metadataJson,64000,nameof(metadataJson));FirstObservedAtUtc=observedAtUtc;LastObservedAtUtc=observedAtUtc;
    }

    public Guid Id{get;private set;} public Guid ProjectId{get;private set;} public DevelopmentGraphNodeType Type{get;private set;}
    public string Key{get;private set;}=string.Empty; public string Name{get;private set;}=string.Empty; public string Status{get;private set;}=string.Empty;
    public string? Provider{get;private set;} public string? ExternalReference{get;private set;} public string MetadataJson{get;private set;}="{}";
    public DateTimeOffset FirstObservedAtUtc{get;private set;} public DateTimeOffset LastObservedAtUtc{get;private set;}

    public void Observe(string name,string status,string? provider,string? externalReference,string metadataJson,DateTimeOffset observedAtUtc)
    {
        Name=Required(name,300,nameof(name));Status=Required(status,80,nameof(status));Provider=Optional(provider,80,nameof(provider));
        ExternalReference=Optional(externalReference,2000,nameof(externalReference));MetadataJson=Required(metadataJson,64000,nameof(metadataJson));
        if(observedAtUtc>LastObservedAtUtc)LastObservedAtUtc=observedAtUtc;
    }

    private static string Required(string? value,int max,string name){var v=value?.Trim()??string.Empty;if(v.Length is <1||v.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);return v;}
    private static string? Optional(string? value,int max,string name){if(string.IsNullOrWhiteSpace(value))return null;var v=value.Trim();if(v.Length>max)throw new ArgumentException($"{name} must be at most {max} characters.",name);return v;}
}
