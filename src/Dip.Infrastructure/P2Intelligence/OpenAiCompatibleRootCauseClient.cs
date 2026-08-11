using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Application.Features.P2Intelligence;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.P2Intelligence;

/// <summary>
/// Optional OpenAI-compatible evidence-grounded root-cause client. It is
/// disabled by default. Only the evidence bundle assembled by DIP is sent.
/// </summary>
public sealed class OpenAiCompatibleRootCauseClient : IAiRootCauseClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly P2IntelligenceOptions _options;

    public OpenAiCompatibleRootCauseClient(IOptions<P2IntelligenceOptions> options)
    {
        _options=options.Value;
        _http=new HttpClient { Timeout=TimeSpan.FromSeconds(Math.Clamp(_options.AiTimeoutSeconds,5,300)) };
    }

    public async Task<AiRootCauseResponse?> AnalyzeAsync(AiRootCauseRequest request,CancellationToken cancellationToken)
    {
        if(!_options.AllowExternalAi||string.IsNullOrWhiteSpace(_options.AiEndpoint))return null;
        using var message=new HttpRequestMessage(HttpMethod.Post,_options.AiEndpoint);
        if(!string.IsNullOrWhiteSpace(_options.AiApiKey))
            message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_options.AiApiKey);
        message.Content=JsonContent.Create(new
        {
            model=_options.AiModel,
            temperature=0.1,
            response_format=new{type="json_object"},
            messages=new object[]
            {
                new{role="system",content=
                    "You are DIP Evidence-Grounded Root Cause Intelligence. Use only the supplied evidence. "
                    +"Never invent a fact, person assessment, or unobserved causal claim. "
                    +"Return JSON with candidates:[{title,confidencePercent,rationale,evidenceIds:[guid]}], "
                    +"suggestedActions:[string], summary:string. If evidence is insufficient, say so."},
                new{role="user",content=request.Question+"\nEVIDENCE_JSON:\n"+request.EvidenceJson}
            }
        });
        using var response=await _http.SendAsync(message,cancellationToken);
        if(!response.IsSuccessStatusCode)return null;
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        string? content=null;
        if(doc.RootElement.TryGetProperty("choices",out var choices)&&choices.ValueKind==JsonValueKind.Array&&choices.GetArrayLength()>0)
        {
            var first=choices[0];
            if(first.TryGetProperty("message",out var m)&&m.TryGetProperty("content",out var c))content=c.GetString();
        }
        content??=doc.RootElement.GetRawText();
        return new(_options.AiModel,content);
    }

    public void Dispose() => _http.Dispose();

}
