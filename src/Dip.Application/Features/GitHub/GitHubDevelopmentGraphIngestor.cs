// Purpose: Coordinates G it Hu bD ev el op me nt Gr ap hI ng es to r application behavior for GitHub.
using System.Text.Json;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.GitHub;

/// <summary>
/// Idempotently projects GitHub push/PR/review/check/workflow/release events into
/// the provider-neutral Development Graph. Webhooks remain the authoritative
/// event source; active GitHub synchronization can fill file/review detail later.
/// </summary>
public sealed class GitHubDevelopmentGraphIngestor
{
    private readonly DevelopmentGraphService _graph;
    public GitHubDevelopmentGraphIngestor(DevelopmentGraphService graph){_graph=graph;}

    public async Task IngestAsync(Guid projectId,string eventName,string payloadJson,CancellationToken ct)
    {
        using var doc=JsonDocument.Parse(payloadJson);var root=doc.RootElement;var repo=Nested(root,"repository","full_name")??"unknown";var repoUrl=Nested(root,"repository","html_url")??repo;var action=Text(root,"action")??string.Empty;var observed=DateTimeOffset.UtcNow;
        switch(eventName.Trim().ToLowerInvariant())
        {
            case "push":
            {
                var revision=Text(root,"after")??Nested(root,"head_commit","id")??"unknown";var branch=(Text(root,"ref")??"refs/heads/unknown").Replace("refs/heads/",string.Empty,StringComparison.OrdinalIgnoreCase);var title=Nested(root,"head_commit","message")??$"Push {revision}";var url=Nested(root,"head_commit","url");var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                if(root.TryGetProperty("commits",out var commits)&&commits.ValueKind==JsonValueKind.Array)foreach(var c in commits.EnumerateArray()){AddFiles(c,"added","added");AddFiles(c,"modified","modified");AddFiles(c,"removed","removed");}
                await _graph.IngestVersionControlChangeAsync(new(projectId,VersionControlProvider.GitHub,repo,repoUrl,revision,branch,title,url,observed,files.Select(x=>new NormalizedChangedFile(x.Key,x.Value)).ToArray()),ct);break;
                void AddFiles(JsonElement commit,string property,string state){if(commit.TryGetProperty(property,out var a)&&a.ValueKind==JsonValueKind.Array)foreach(var item in a.EnumerateArray()){var p=item.GetString();if(!string.IsNullOrWhiteSpace(p))files[p]=state;}}
            }
            case "pull_request":
            {
                var number=NestedInt(root,"pull_request","number")??Int(root,"number");var sha=Nested(root,"pull_request","head","sha")??"unknown";var branch=Nested(root,"pull_request","head","ref")??"unknown";var title=Nested(root,"pull_request","title")??$"Pull request #{number}";var url=Nested(root,"pull_request","html_url");var merged=NestedBool(root,"pull_request","merged")??false;await _graph.IngestVersionControlChangeAsync(new(projectId,VersionControlProvider.GitHub,repo,repoUrl,sha,branch,title,url,observed,[],number,[],null,merged),ct);
                var pr=await _graph.FindNodeAsync(projectId,DevelopmentGraphNodeType.PullRequest,$"pr:{repo}:{number}",ct);if(pr is not null)await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.PullRequest,pr.Key,pr.Name,merged?"Merged":Nested(root,"pull_request","state")??action,"GitHub",url,pr.MetadataJson),ct);break;
            }
            case "pull_request_review":
            {
                var number=NestedInt(root,"pull_request","number")??0;var reviewer=Nested(root,"review","user","login")??"reviewer";var state=Nested(root,"review","state")??action;var url=Nested(root,"review","html_url")??Nested(root,"pull_request","html_url");var pr=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.PullRequest,$"pr:{repo}:{number}",$"PR #{number}","Open","GitHub",Nested(root,"pull_request","html_url"),"{}"),ct);var review=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.Review,$"review:{repo}:{number}:{reviewer}",reviewer,state,"GitHub",url,JsonSerializer.Serialize(new{number,reviewer,state,action})),ct);await _graph.LinkAsync(new(projectId,review.Id,pr.Id,DevelopmentGraphEdgeType.Reviews,80m,state.Equals("changes_requested",StringComparison.OrdinalIgnoreCase),"{}"),ct);break;
            }
            case "workflow_run":
            {
                var runId=Nested(root,"workflow_run","id")??Nested(root,"workflow_run","run_number")??"unknown";var pipeline=Nested(root,"workflow_run","name")??"GitHub Actions";var status=Nested(root,"workflow_run","conclusion")??Nested(root,"workflow_run","status")??action;var sha=Nested(root,"workflow_run","head_sha");var branch=Nested(root,"workflow_run","head_branch");var url=Nested(root,"workflow_run","html_url");await _graph.IngestCiRunAsync(new(projectId,ExternalCiProvider.GitHubActions,repo,pipeline,runId,status,sha,branch,url,observed),ct);break;
            }
            case "check_run":
            {
                var runId=Nested(root,"check_run","id")??"unknown";var pipeline=Nested(root,"check_run","name")??"GitHub Check";var status=Nested(root,"check_run","conclusion")??Nested(root,"check_run","status")??action;var sha=Nested(root,"check_run","head_sha");var url=Nested(root,"check_run","html_url");await _graph.IngestCiRunAsync(new(projectId,ExternalCiProvider.GitHubActions,repo,pipeline,runId,status,sha,null,url,observed),ct);break;
            }
            case "release":
            {
                var tag=Nested(root,"release","tag_name")??"untagged";var target=Nested(root,"release","target_commitish")??tag;var url=Nested(root,"release","html_url");var release=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.Release,$"release:{repo}:{tag}",tag,action,"GitHub",url,JsonSerializer.Serialize(new{tag,target,action})),ct);var repoNode=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.Repository,$"repo:{repo}",repo,"Active","GitHub",repoUrl,"{}"),ct);await _graph.LinkAsync(new(projectId,repoNode.Id,release.Id,DevelopmentGraphEdgeType.Contains,90m,true,"{}"),ct);break;
            }
            case "deployment":
            case "deployment_status":
            {
                var id=Nested(root,"deployment","id")??"unknown";var env=Nested(root,"deployment","environment")??"deployment";var status=eventName.Equals("deployment_status",StringComparison.OrdinalIgnoreCase)?Nested(root,"deployment_status","state")??action:action;var dep=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.Deployment,$"deployment:{repo}:{id}",env,status,"GitHub",Nested(root,"deployment_status","target_url"),JsonSerializer.Serialize(new{id,env,status})),ct);var sha=Nested(root,"deployment","sha");if(!string.IsNullOrWhiteSpace(sha)){var commit=await _graph.FindNodeAsync(projectId,DevelopmentGraphNodeType.Commit,$"commit:{repo}:{sha}",ct);if(commit is not null)await _graph.LinkAsync(new(projectId,commit.Id,dep.Id,DevelopmentGraphEdgeType.Deploys,90m,false,"{}"),ct);}break;
            }
        }
    }

    private static string? Text(JsonElement e,string property)=>e.TryGetProperty(property,out var n)?n.ValueKind switch{JsonValueKind.String=>n.GetString(),JsonValueKind.Number=>n.GetRawText(),_=>null}:null;
    private static int? Int(JsonElement e,string property)=>e.TryGetProperty(property,out var n)&&n.TryGetInt32(out var v)?v:null;
    private static string? Nested(JsonElement e,params string[] path){var current=e;foreach(var p in path){if(current.ValueKind!=JsonValueKind.Object||!current.TryGetProperty(p,out current))return null;}return current.ValueKind switch{JsonValueKind.String=>current.GetString(),JsonValueKind.Number=>current.GetRawText(),JsonValueKind.True=>"true",JsonValueKind.False=>"false",_=>null};}
    private static int? NestedInt(JsonElement e,params string[] path){var current=e;foreach(var p in path){if(current.ValueKind!=JsonValueKind.Object||!current.TryGetProperty(p,out current))return null;}return current.TryGetInt32(out var v)?v:null;}
    private static bool? NestedBool(JsonElement e,params string[] path){var current=e;foreach(var p in path){if(current.ValueKind!=JsonValueKind.Object||!current.TryGetProperty(p,out current))return null;}return current.ValueKind is JsonValueKind.True or JsonValueKind.False?current.GetBoolean():null;}
}
