// Purpose: Integrates external or runtime capabilities through J en ki ns CI Co nn ec to r.
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Integrations;

public sealed class JenkinsCiConnector:IExternalCiConnector
{
    private readonly JenkinsOptions _options;
        public ExternalCiProvider Provider=>ExternalCiProvider.Jenkins;
        public JenkinsCiConnector(IOptions<JenkinsOptions> options){_options=options.Value;
        }
    public async Task<NormalizedCiRun> FetchAsync(ExternalCiSyncRequest request,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(_options.BaseUrl))throw new InvalidOperationException("Jenkins:BaseUrl is required.");
            var job=request.JobPath??request.PipelineKey;
            if(string.IsNullOrWhiteSpace(job))throw new ArgumentException("Jenkins job path is required.",nameof(request));
            var build=request.BuildNumber??(int.TryParse(request.RunId,out var parsed)?parsed:throw new ArgumentException("Jenkins build number is required.",nameof(request)));
            var jobPath=string.Join('/',job.Split('/',StringSplitOptions.RemoveEmptyEntries).Select(x=>$"job/{Uri.EscapeDataString(x)}"));
            var url=$"{_options.BaseUrl.TrimEnd('/')}/{jobPath}/{build}/api/json?tree=number,result,building,url,timestamp,duration,actions[parameters[name,value]],fullDisplayName";
            using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds,5,300))};
            if(!string.IsNullOrWhiteSpace(_options.User)&&!string.IsNullOrWhiteSpace(_options.ApiToken))client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.User}:{_options.ApiToken}")));
            using var response=await client.GetAsync(url,ct);
            response.EnsureSuccessStatusCode();
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root=doc.RootElement;
            var result=GetString(root,"result")??(GetBool(root,"building")?"Running":"Unknown");
            var runUrl=GetString(root,"url");
            var timestamp=GetLong(root,"timestamp");
            var observed=timestamp.HasValue?DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value):DateTimeOffset.UtcNow;
            var revision=FindParameter(root,"GIT_COMMIT")??FindParameter(root,"GIT_COMMIT_SHA")??FindParameter(root,"REVISION");
            var branch=FindParameter(root,"GIT_BRANCH")??FindParameter(root,"BRANCH_NAME");
            return new(request.ProjectId,Provider,request.RepositoryKey,request.PipelineKey,build.ToString(System.Globalization.CultureInfo.InvariantCulture),result,revision,branch,runUrl,observed,MetadataJson:JsonSerializer.Serialize(new{DurationMs=GetLong(root,"duration"),DisplayName=GetString(root,"fullDisplayName")}));
    }
    private static string? FindParameter(JsonElement root,string name){if(!root.TryGetProperty("actions",out var actions)||actions.ValueKind!=JsonValueKind.Array)return null;
        foreach(var action in actions.EnumerateArray()){if(!action.TryGetProperty("parameters",out var ps)||ps.ValueKind!=JsonValueKind.Array)continue;
        foreach(var p in ps.EnumerateArray())if(string.Equals(GetString(p,"name"),name,StringComparison.OrdinalIgnoreCase)&&p.TryGetProperty("value",out var value))return value.ValueKind==JsonValueKind.String?value.GetString():value.GetRawText();
        }return null;
        }
    private static string? GetString(JsonElement e,string p)=>e.TryGetProperty(p,out var n)&&n.ValueKind==JsonValueKind.String?n.GetString():null;
        private static bool GetBool(JsonElement e,string p)=>e.TryGetProperty(p,out var n)&&(n.ValueKind is JsonValueKind.True or JsonValueKind.False)&&n.GetBoolean();
        private static long? GetLong(JsonElement e,string p)=>e.TryGetProperty(p,out var n)&&n.TryGetInt64(out var v)?v:null;
}
