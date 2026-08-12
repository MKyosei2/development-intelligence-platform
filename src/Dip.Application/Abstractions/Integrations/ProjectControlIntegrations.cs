using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Integrations;

public sealed record VersionControlSyncRequest(
    Guid ProjectId,VersionControlProvider Provider,string RepositoryKey,string RepositoryLocator,string Revision,
    int? PullRequestNumber=null,string? WorkingDirectory=null);

public interface IVersionControlConnector
{
    VersionControlProvider Provider{get;}
    Task<NormalizedVersionControlChange> FetchAsync(VersionControlSyncRequest request,CancellationToken cancellationToken);
}

public sealed record ExternalCiSyncRequest(
    Guid ProjectId,ExternalCiProvider Provider,string RepositoryKey,string PipelineKey,string RunId,
    string? JobPath=null,int? BuildNumber=null);

public interface IExternalCiConnector
{
    ExternalCiProvider Provider{get;}
    Task<NormalizedCiRun> FetchAsync(ExternalCiSyncRequest request,CancellationToken cancellationToken);
}
