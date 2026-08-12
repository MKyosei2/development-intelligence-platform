using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

public sealed record UpsertDevelopmentGraphNodeCommand(
    Guid ProjectId,DevelopmentGraphNodeType Type,string Key,string Name,string Status="Observed",
    string? Provider=null,string? ExternalReference=null,string MetadataJson="{}");

public sealed record LinkDevelopmentGraphNodesCommand(
    Guid ProjectId,Guid SourceNodeId,Guid TargetNodeId,DevelopmentGraphEdgeType Type,
    decimal Weight=50m,bool Critical=false,string MetadataJson="{}");

public sealed record DevelopmentGraphNodeDto(Guid Id,string Type,string Key,string Name,string Status,string? Provider,string? ExternalReference,string MetadataJson,DateTimeOffset LastObservedAtUtc);
public sealed record DevelopmentGraphEdgeDto(Guid Id,Guid SourceNodeId,Guid TargetNodeId,string Type,decimal Weight,bool Critical,string MetadataJson,DateTimeOffset LastObservedAtUtc);
public sealed record DevelopmentGraphDto(Guid ProjectId,IReadOnlyList<DevelopmentGraphNodeDto> Nodes,IReadOnlyList<DevelopmentGraphEdgeDto> Edges);

public sealed record NormalizedChangedFile(string Path,string Status,int Additions=0,int Deletions=0);
public sealed record NormalizedReview(string Reviewer,string State,DateTimeOffset? SubmittedAtUtc=null);
public sealed record NormalizedVersionControlChange(
    Guid ProjectId,VersionControlProvider Provider,string RepositoryKey,string RepositoryLocator,string Revision,
    string Branch,string Title,string? Url,DateTimeOffset ObservedAtUtc,IReadOnlyList<NormalizedChangedFile> Files,
    int? PullRequestNumber=null,IReadOnlyList<NormalizedReview>? Reviews=null,string? ReleaseTag=null,bool IsMerge=false);

public sealed record NormalizedCiRun(
    Guid ProjectId,ExternalCiProvider Provider,string RepositoryKey,string PipelineKey,string RunId,string Status,
    string? Revision,string? Branch,string? Url,DateTimeOffset ObservedAtUtc,int PassedTests=0,int FailedTests=0,int SkippedTests=0,
    string MetadataJson="{}");
