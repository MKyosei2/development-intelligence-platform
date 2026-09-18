// Purpose: Orchestrates the DIP workflow for P ro je ct So ur ce In ge st io nS er vi ce.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectIntelligence;

public sealed class ProjectSourceIngestionService
{
    private readonly IEnumerable<IProjectSourceAdapter> _adapters;private readonly DevelopmentGraphService _graph;private readonly IDevelopmentGraphRepository _repo;private readonly IEvidenceRepository _evidence;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;private readonly ProjectEntityResolutionService _resolver;
    public ProjectSourceIngestionService(IEnumerable<IProjectSourceAdapter> adapters,DevelopmentGraphService graph,IDevelopmentGraphRepository repo,IEvidenceRepository evidence,IUnitOfWork uow,ISystemClock clock,ProjectEntityResolutionService resolver)
    {_adapters=adapters;_graph=graph;_repo=repo;_evidence=evidence;_uow=uow;_clock=clock;_resolver=resolver;}

    public IReadOnlyList<(string Provider,bool Configured)> Providers(Guid projectId)=>_adapters.Select(x=>(Provider:x.ProviderName,Configured:x.IsConfiguredFor(projectId))).OrderBy(x=>x.Provider,StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<ProjectSourceSyncResult> SynchronizeAsync(ProjectSourcePullCommand command,CancellationToken ct)
    {
        if(command.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(command));var adapter=_adapters.FirstOrDefault(x=>x.ProviderName.Equals(command.Provider,StringComparison.OrdinalIgnoreCase))??throw new NotSupportedException($"Project source '{command.Provider}' is not registered.");if(!adapter.IsConfiguredFor(command.ProjectId))throw new InvalidOperationException($"Project source '{adapter.ProviderName}' is not configured.");
        var beforeResolution=command.DryRun?null:await _resolver.ResolveProjectAsync(command.ProjectId,false,ct);var observations=await adapter.PullAsync(command.ProjectId,ct);var changed=0;var unchanged=0;var evidenceCount=0;var warnings=new List<string>();
        foreach(var obs in observations.Take(10000))
        {
            ct.ThrowIfCancellationRequested();var key=ObservationKey(obs.Provider,obs.ExternalId);var existing=await _repo.FindNodeAsync(command.ProjectId,DevelopmentGraphNodeType.ExternalObservation,key,ct);if(existing is not null&&Revision(existing).Equals(obs.Revision,StringComparison.Ordinal)){unchanged++;continue;}
            changed++;if(command.DryRun)continue;var metadata=ObservationMetadata(obs);var node=await _graph.UpsertNodeAsync(new(command.ProjectId,DevelopmentGraphNodeType.ExternalObservation,key,Clip(obs.Title,300),obs.Status,obs.Provider,obs.Url,metadata),ct);
            var sourceType=obs.Provider.Equals("Redmine",StringComparison.OrdinalIgnoreCase)?EvidenceSourceType.IssueTracker:EvidenceSourceType.Collaboration;var subject=InferSubject(obs)??$"source:{obs.Provider}:{obs.ExternalId}";var confidence=obs.Provider.Equals("Redmine",StringComparison.OrdinalIgnoreCase)?EvidenceConfidence.Medium:HasEvidenceReference(obs.References)?EvidenceConfidence.Medium:EvidenceConfidence.Low;
            await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,subject,sourceType,$"source.{obs.Provider.ToLowerInvariant()}.{obs.Kind}",Outcome(obs),Clip(obs.Title,200),Clip(obs.Body??$"{obs.Status} {obs.ProgressPercent:0.#}%",4000),confidence,obs.Url,TrimJson(metadata,16000),obs.UpdatedAtUtc,_clock.UtcNow),ct);evidenceCount++;
        }
        if(observations.Count>10000)warnings.Add("Source result was capped at 10,000 observations for one synchronization run.");if(!command.DryRun&&evidenceCount>0)await _uow.SaveChangesAsync(ct);var resolution=command.DryRun?new EntityResolutionOverviewDto(command.ProjectId,0,0,0,[],_clock.UtcNow):await _resolver.ResolveProjectAsync(command.ProjectId,true,ct);var newlyResolved=command.DryRun?0:Math.Max(0,resolution.Resolved-(beforeResolution?.Resolved??0));
        return new(command.ProjectId,adapter.ProviderName,observations.Count,changed,unchanged,evidenceCount,newlyResolved,command.DryRun,warnings,_clock.UtcNow);
    }

    public async Task<CollaborationEvidenceResult> IngestCollaborationAsync(CollaborationEvidenceCommand command,CancellationToken ct)
    {
        if(command.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(command));if(string.IsNullOrWhiteSpace(command.Provider)||string.IsNullOrWhiteSpace(command.ExternalId)||string.IsNullOrWhiteSpace(command.Text))throw new ArgumentException("Provider, ExternalId and Text are required.",nameof(command));
        var refs=(command.References??ExtractReferences(command.Text)).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();var revision=string.IsNullOrWhiteSpace(command.Revision)?Hash(command.Text+"\n"+string.Join('\n',refs)):command.Revision!.Trim();var status=InferStatus(command.Text);var progress=InferProgress(command.Text);var title=Clip(FirstLine(command.Text),300);
        var observation=new ProjectSourceObservation(command.Provider,command.ExternalId,"progress-message",title,command.Text,status,progress,command.Author,null,null,command.ObservedAtUtc==default?_clock.UtcNow:command.ObservedAtUtc,revision,command.Url,refs,new Dictionary<string,string>{{"Channel",command.Channel??""},{"Author",command.Author??""}});
        var key=ObservationKey(command.Provider,command.ExternalId);var existing=await _repo.FindNodeAsync(command.ProjectId,DevelopmentGraphNodeType.ExternalObservation,key,ct);if(existing is null||!Revision(existing).Equals(revision,StringComparison.Ordinal))
        {
            var metadata=ObservationMetadata(observation);await _graph.UpsertNodeAsync(new(command.ProjectId,DevelopmentGraphNodeType.ExternalObservation,key,title,status,command.Provider,command.Url,metadata),ct);var subject=InferSubject(observation)??$"source:{command.Provider}:{command.ExternalId}";var backed=HasEvidenceReference(refs);await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,subject,EvidenceSourceType.Collaboration,$"collaboration.{command.Provider.ToLowerInvariant()}.progress",EvidenceOutcome.Informational,title,Clip(command.Text,4000),backed?EvidenceConfidence.Medium:EvidenceConfidence.Low,command.Url,TrimJson(metadata,16000),observation.UpdatedAtUtc,_clock.UtcNow),ct);await _uow.SaveChangesAsync(ct);
        }
        var resolution=await _resolver.ResolveProjectAsync(command.ProjectId,true,ct);var target=resolution.Items.FirstOrDefault(x=>x.SourceKey.Equals(Strip(key),StringComparison.OrdinalIgnoreCase)&&x.Resolution=="Resolved");var autoResolved=target is null?0:1;return new(key,target?.TargetKey??InferSubject(observation)??key,HasEvidenceReference(refs)?"Medium":"Low",HasEvidenceReference(refs),refs,autoResolved,_clock.UtcNow);
    }

    internal static string ObservationKey(string provider,string externalId)=>$"source:{Safe(provider)}:{Safe(externalId)}";
    public static IReadOnlyList<string> ExtractReferences(string text)=>Regex.Matches(text,@"https?://[^\s<>\]\)]+|\b(?:DIP|TASK|REDMINE|RM)[:#-][A-Za-z0-9_.-]+",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant).Select(x=>x.Value.TrimEnd('.',',',';',':')).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
    private static string ObservationMetadata(ProjectSourceObservation o){var refs=(o.References??[]).Take(100).ToArray();var fields=o.Fields is null?null:o.Fields.Take(100).ToDictionary(x=>x.Key,x=>Clip(x.Value,1000),StringComparer.OrdinalIgnoreCase);var raw=JsonSerializer.Serialize(new{o.Provider,o.ExternalId,o.Kind,o.Title,o.Body,o.Status,o.ProgressPercent,o.Owner,o.StartAtUtc,o.DueAtUtc,o.UpdatedAtUtc,o.Revision,o.Url,References=refs,Fields=fields});var sha=Hash(raw);return JsonSerializer.Serialize(new{o.Provider,o.ExternalId,o.Kind,o.Status,o.ProgressPercent,o.Owner,o.StartAtUtc,o.DueAtUtc,o.UpdatedAtUtc,o.Revision,o.Url,References=refs,Fields=fields,RawSha256=sha,Provenance=new{Provider=o.Provider,SourceId=o.ExternalId,Revision=o.Revision,Url=o.Url,ObservedAtUtc=o.UpdatedAtUtc,RawSha256=sha}});}
    private static string? InferSubject(ProjectSourceObservation o){foreach(var r in o.References){var m=Regex.Match(r,@"(?:DIP|TASK)[:#-](?<key>[A-Za-z0-9_.-]+)",RegexOptions.IgnoreCase);if(m.Success)return m.Groups["key"].Value;}var title=Regex.Match(o.Title,@"\b(?:DIP|TASK)[:#-](?<key>[A-Za-z0-9_.-]+)\b",RegexOptions.IgnoreCase);return title.Success?title.Groups["key"].Value:null;}
    private static EvidenceOutcome Outcome(ProjectSourceObservation o)=>IsDone(o.Status)||o.ProgressPercent>=100m?EvidenceOutcome.Succeeded:o.Status.Contains("blocked",StringComparison.OrdinalIgnoreCase)||o.Status.Contains("reject",StringComparison.OrdinalIgnoreCase)?EvidenceOutcome.Warning:EvidenceOutcome.Informational;
    private static bool IsDone(string status)=>status.Contains("done",StringComparison.OrdinalIgnoreCase)||status.Contains("closed",StringComparison.OrdinalIgnoreCase)||status.Contains("complete",StringComparison.OrdinalIgnoreCase)||status.Contains("終了",StringComparison.OrdinalIgnoreCase)||status.Contains("完了",StringComparison.OrdinalIgnoreCase);
    private static bool HasEvidenceReference(IEnumerable<string> refs)=>refs.Any(r=>r.StartsWith("file:",StringComparison.OrdinalIgnoreCase)||r.Contains("github.com",StringComparison.OrdinalIgnoreCase)||r.Contains("gitlab",StringComparison.OrdinalIgnoreCase)||r.Contains("build",StringComparison.OrdinalIgnoreCase)||r.Contains("ci",StringComparison.OrdinalIgnoreCase)||r.EndsWith(".mp4",StringComparison.OrdinalIgnoreCase)||r.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||r.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase));
    private static string InferStatus(string text){if(Regex.IsMatch(text,@"\b(done|complete|completed|finished|完了|終了)\b",RegexOptions.IgnoreCase))return "ClaimedCompleted";if(Regex.IsMatch(text,@"\b(blocked|blocker|詰ま|ブロック|待ち)\b",RegexOptions.IgnoreCase))return "Blocked";return "ProgressReported";}
    private static decimal? InferProgress(string text){var m=Regex.Match(text,@"(?<p>\d{1,3}(?:\.\d+)?)\s*[%％]",RegexOptions.CultureInvariant);return m.Success&&decimal.TryParse(m.Groups["p"].Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var p)?Math.Clamp(p,0m,100m):null;}
    private static string Revision(Dip.Domain.Entities.DevelopmentGraphNode n){try{using var d=JsonDocument.Parse(n.MetadataJson);return d.RootElement.TryGetProperty("Revision",out var r)&&r.ValueKind==JsonValueKind.String?r.GetString()??"":"";}catch{return "";}}
    private static string FirstLine(string text)=>text.Replace("\r","").Split('\n',2)[0].Trim();private static string Safe(string value){var safe=Regex.Replace(value.Trim(),@"[^A-Za-z0-9_.-]+","-").Trim('-');if(string.IsNullOrWhiteSpace(safe))safe="item";return safe[..Math.Min(180,safe.Length)];}
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();private static string Clip(string value,int max){var v=value.Trim();return v[..Math.Min(max,v.Length)];}private static string TrimJson(string value,int max)=>value.Length<=max?value:JsonSerializer.Serialize(new{Truncated=true,Sha256=Hash(value),Preview=value[..Math.Min(12000,value.Length)]});private static string Strip(string value){var parts=value.Split(':',StringSplitOptions.RemoveEmptyEntries);return parts.Length>0?parts[^1]:value;}
}
