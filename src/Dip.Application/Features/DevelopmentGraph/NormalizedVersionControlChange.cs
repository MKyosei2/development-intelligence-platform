// Purpose: Defines the NormalizedVersionControlChange application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record NormalizedVersionControlChange(Guid ProjectId,VersionControlProvider Provider,string RepositoryKey,string RepositoryLocator,string Revision,string Branch,string Title,string? Url,DateTimeOffset ObservedAtUtc,IReadOnlyList<NormalizedChangedFile> Files,int? PullRequestNumber=null,IReadOnlyList<NormalizedReview>? Reviews=null,string? ReleaseTag=null,bool IsMerge=false);
