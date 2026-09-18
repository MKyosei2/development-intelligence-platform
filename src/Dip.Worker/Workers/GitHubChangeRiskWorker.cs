// Purpose: Runs background processing for G it Hu bC ha ng eR is kW or ke r.
using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.ChangeRisk;
using Dip.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

/// <summary>Creates deterministic Change Risk evidence from GitHub push payloads without blocking webhook ingestion.</summary>
public sealed class GitHubChangeRiskWorker(IServiceScopeFactory scopes,IQueueClient queue,IOptions<QueueOptions> options,ILogger<GitHubChangeRiskWorker> logger):BackgroundService
{
    private readonly QueueOptions _options=options.Value;
    private readonly string _consumer=$"{Environment.MachineName}-change-risk-{Guid.NewGuid():N}";
    private string Group=>$"{_options.ConsumerGroup}:change-risk";

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                var envelope=await queue.ReadAsync("github-webhooks",Group,_consumer,TimeSpan.FromMilliseconds(Math.Max(100,_options.BlockMilliseconds)),stop);if(envelope is null)continue;
                try{await ProcessAsync(envelope,stop);await queue.AcknowledgeAsync("github-webhooks",Group,envelope.MessageId,stop);}
                catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
                catch(Exception ex){logger.LogError(ex,"Automatic GitHub change-risk assessment failed for {MessageId}.",envelope.MessageId);if(envelope.Attempt+1>=Math.Max(1,_options.MaxDeliveryCount))await queue.MoveToDeadLetterAsync("github-webhooks",Group,_options.DeadLetterStreamName,envelope,ex.Message,stop);else await queue.RetryAsync("github-webhooks",Group,envelope,stop);}
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"GitHub change-risk worker loop failed.");await Task.Delay(1000,stop);}
        }
    }

    private async Task ProcessAsync(QueueEnvelope envelope,CancellationToken ct)
    {
        using var wrapper=JsonDocument.Parse(envelope.PayloadJson);var payload=wrapper.RootElement.TryGetProperty("payload",out var nested)?nested:wrapper.RootElement;
        if(payload.ValueKind==JsonValueKind.String){using var decoded=JsonDocument.Parse(payload.GetString()??"{}");payload=decoded.RootElement.Clone();}
        var eventName=GetString(payload,"eventName")??GetString(payload,"EventName");if(!string.Equals(eventName,"push",StringComparison.OrdinalIgnoreCase))return;
        var raw=GetString(payload,"payloadJson")??GetString(payload,"PayloadJson");if(string.IsNullOrWhiteSpace(raw))return;
        using var doc=JsonDocument.Parse(raw);var root=doc.RootElement;var repository=ReadNested(root,"repository","html_url")??ReadNested(root,"repository","full_name");if(string.IsNullOrWhiteSpace(repository))return;
        using var scope=scopes.CreateScope();var projects=scope.ServiceProvider.GetRequiredService<IProjectRepository>();var project=(await projects.ListAsync(ct)).FirstOrDefault(x=>NormalizeRepository(x.RepositoryUrl)==NormalizeRepository(repository));if(project is null)return;
        var files=ReadChangedFiles(root);var commit=ReadString(root,"after")??ReadNested(root,"head_commit","id")??"unknown";if(commit.Length>64)commit=commit[..64];if(string.Equals(commit,"unknown",StringComparison.Ordinal))return;
        var incidents=scope.ServiceProvider.GetRequiredService<IIncidentRepository>();var related=(await incidents.ListRecentAsync(project.Id,200,ct)).Count(x=>files.Any(f=>x.SubjectKey.Contains(Path.GetFileName(f),StringComparison.OrdinalIgnoreCase)));
        var service=scope.ServiceProvider.GetRequiredService<ChangeRiskIntelligenceService>();var touchesPerformance=files.Any(ContainsPerformance);var touchesSecurity=files.Any(ContainsSecurity);var touchesSchema=files.Any(ContainsSchema);
        await service.AssessAsync(new ChangeRiskRequest(project.Id,commit,files.Count,0,null,related,0,files,touchesPerformance,touchesSecurity,touchesSchema),ct);
    }

    private static List<string> ReadChangedFiles(JsonElement root)
    {
        var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);if(!root.TryGetProperty("commits",out var commits)||commits.ValueKind!=JsonValueKind.Array)return [];
        foreach(var commit in commits.EnumerateArray())foreach(var property in new[]{"added","modified","removed"})if(commit.TryGetProperty(property,out var values)&&values.ValueKind==JsonValueKind.Array)foreach(var value in values.EnumerateArray()){var path=value.GetString()?.Replace('\\','/').Trim();if(!string.IsNullOrWhiteSpace(path))set.Add(path);}
        return set.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList();
    }
    private static bool ContainsPerformance(string p)=>ContainsAny(p,"render","shader","gpu","performance","frame","physics","animation","network");
    private static bool ContainsSecurity(string p)=>ContainsAny(p,"auth","security","token","secret","crypto","permission","authorization");
    private static bool ContainsSchema(string p)=>ContainsAny(p,"migration","schema","contract","proto","openapi","database","persistence");
    private static bool ContainsAny(string p,params string[] tokens)=>tokens.Any(t=>p.Contains(t,StringComparison.OrdinalIgnoreCase));
    private static string? GetString(JsonElement root,string name)=>root.TryGetProperty(name,out var n)&&n.ValueKind==JsonValueKind.String?n.GetString():null;
    private static string? ReadString(JsonElement root,string name)=>root.TryGetProperty(name,out var n)?n.GetString():null;
    private static string? ReadNested(JsonElement root,string first,string second)=>root.TryGetProperty(first,out var n)&&n.TryGetProperty(second,out var v)?v.GetString():null;
    private static string NormalizeRepository(string value){var v=value.Trim().TrimEnd('/');if(Uri.TryCreate(v,UriKind.Absolute,out var uri))v=uri.AbsolutePath.Trim('/');if(v.EndsWith(".git",StringComparison.OrdinalIgnoreCase))v=v[..^4];return v.ToLowerInvariant();}
}
