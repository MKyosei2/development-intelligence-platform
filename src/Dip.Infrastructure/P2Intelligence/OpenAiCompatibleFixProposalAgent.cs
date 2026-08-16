using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.P2Intelligence;

/// <summary>Optional, evidence-grounded fix proposal agent. It never edits a workspace.</summary>
public sealed partial class OpenAiCompatibleFixProposalAgent:IFixProposalAgent,IDisposable
{
    private readonly HttpClient _http;private readonly P2IntelligenceOptions _o;
    public OpenAiCompatibleFixProposalAgent(IOptions<P2IntelligenceOptions> options){_o=options.Value;_http=new HttpClient{Timeout=TimeSpan.FromSeconds(Math.Clamp(_o.AiTimeoutSeconds,5,300))};}
    public async Task<FixProposalResponse?> ProposeAsync(FixProposalRequest r,CancellationToken ct)
    {
        if(!_o.AllowExternalAi||!_o.AllowFixProposal||string.IsNullOrWhiteSpace(_o.AiEndpoint))return null;var allowed=GuidRegex().Matches(r.EvidenceJson).Cast<Match>().Select(m=>Guid.Parse(m.Value)).ToHashSet();
        using var req=new HttpRequestMessage(HttpMethod.Post,_o.AiEndpoint);if(!string.IsNullOrWhiteSpace(_o.AiApiKey))req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_o.AiApiKey);
        req.Content=JsonContent.Create(new{model=_o.AiModel,temperature=0.0,response_format=new{type="json_object"},messages=new object[]{new{role="system",content="You are the DIP Evidence-Grounded Fix Proposal Agent. Use only supplied evidence. Never claim a fix is verified. Return JSON: {summary:string, unifiedDiff:string, regressionTests:[{relativePath,content,purpose}], evidenceIds:[guid]}. The diff must be a standard unified git diff and must not add secrets, disable tests, weaken authorization, or suppress diagnostics. If evidence is insufficient return an empty unifiedDiff."},new{role="user",content=r.Question+"\nEVIDENCE_JSON:\n"+r.EvidenceJson+"\nREPOSITORY_CONTEXT:\n"+(r.RepositoryContext??"")}}});
        using var res=await _http.SendAsync(req,ct);if(!res.IsSuccessStatusCode)return null;using var envelope=JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));string json;if(envelope.RootElement.TryGetProperty("choices",out var choices)&&choices.ValueKind==JsonValueKind.Array&&choices.GetArrayLength()>0)json=choices[0].GetProperty("message").GetProperty("content").GetString()??"{}";else json=envelope.RootElement.GetRawText();using var doc=JsonDocument.Parse(json);var root=doc.RootElement;var summary=root.TryGetProperty("summary",out var sm)?sm.GetString()??"":"";var diff=root.TryGetProperty("unifiedDiff",out var df)?df.GetString()??"":"";var tests=new List<RegressionTestDraft>();if(root.TryGetProperty("regressionTests",out var ta)&&ta.ValueKind==JsonValueKind.Array)foreach(var t in ta.EnumerateArray().Take(20)){var path=t.TryGetProperty("relativePath",out var p)?p.GetString():null;var content=t.TryGetProperty("content",out var c)?c.GetString():null;var purpose=t.TryGetProperty("purpose",out var pp)?pp.GetString():null;if(!string.IsNullOrWhiteSpace(path)&&content is not null)tests.Add(new(path,content,purpose??"Regression coverage for proposed fix."));}var ids=new List<Guid>();if(root.TryGetProperty("evidenceIds",out var ea)&&ea.ValueKind==JsonValueKind.Array)foreach(var e in ea.EnumerateArray())if(Guid.TryParse(e.GetString(),out var id)&&allowed.Contains(id))ids.Add(id);if(ids.Count==0||string.IsNullOrWhiteSpace(diff))return null;return new("OpenAI-compatible",_o.AiModel,summary,diff,tests,ids.Distinct().ToArray());
    }
    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}")]private static partial Regex GuidRegex();
    public void Dispose()=>_http.Dispose();
}
