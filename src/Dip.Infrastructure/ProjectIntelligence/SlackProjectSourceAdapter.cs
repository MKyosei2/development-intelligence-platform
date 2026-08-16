using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.ProjectIntelligence;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.ProjectIntelligence;

public sealed class SlackProjectSourceAdapter:IProjectSourceAdapter,IDisposable
{
    private readonly ProjectIntelligenceSourceOptions.SlackSourceOptions _o;private readonly HttpClient _http=new(){Timeout=TimeSpan.FromSeconds(30)};public string ProviderName=>"Slack";public bool IsConfigured=>!string.IsNullOrWhiteSpace(_o.BotToken)&&(_o.ChannelIds.Length>0||_o.ProjectChannelIds.Count>0);public bool IsConfiguredFor(Guid projectId)=>!string.IsNullOrWhiteSpace(_o.BotToken)&&_o.Channels(projectId).Any(x=>!string.IsNullOrWhiteSpace(x));
    public SlackProjectSourceAdapter(IOptions<ProjectIntelligenceSourceOptions> options)=>_o=options.Value.Slack;
    public async Task<IReadOnlyList<ProjectSourceObservation>> PullAsync(Guid projectId,CancellationToken ct)
    {
        if(!IsConfigured)throw new InvalidOperationException("Slack source requires BotToken and ChannelIds/ProjectChannelIds.");var channels=_o.Channels(projectId).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();if(channels.Length==0)throw new InvalidOperationException($"Slack channel mapping is missing for DIP project {projectId}.");var result=new List<ProjectSourceObservation>();var oldest=DateTimeOffset.UtcNow.AddDays(-Math.Clamp(_o.HistoryDays,1,365)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        foreach(var channel in channels)
        {
            string? cursor=null;for(var page=0;page<Math.Clamp(_o.MaxPagesPerChannel,1,100);page++)
            {
                var uri=$"https://slack.com/api/conversations.history?channel={Uri.EscapeDataString(channel)}&limit=200&oldest={oldest}"+(string.IsNullOrWhiteSpace(cursor)?"":$"&cursor={Uri.EscapeDataString(cursor)}");using var req=new HttpRequestMessage(HttpMethod.Get,uri);req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_o.BotToken);using var res=await _http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);var raw=await res.Content.ReadAsStringAsync(ct);if(!res.IsSuccessStatusCode)throw new InvalidOperationException($"Slack history pull failed ({(int)res.StatusCode}).");using var doc=JsonDocument.Parse(raw);if(!doc.RootElement.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True)throw new InvalidOperationException("Slack API rejected conversations.history: "+(doc.RootElement.TryGetProperty("error",out var er)?er.GetString():"unknown_error"));if(!doc.RootElement.TryGetProperty("messages",out var messages)||messages.ValueKind!=JsonValueKind.Array)break;
                foreach(var m in messages.EnumerateArray()){var text=m.TryGetProperty("text",out var te)?te.GetString()??"":"";if(string.IsNullOrWhiteSpace(text)||!Relevant(text,m))continue;var ts=m.TryGetProperty("ts",out var t)?t.GetString()??Guid.NewGuid().ToString("N"):Guid.NewGuid().ToString("N");var at=ParseSlackTs(ts);var author=m.TryGetProperty("user",out var u)?u.GetString():m.TryGetProperty("username",out var un)?un.GetString():null;var refs=Extract(text).ToList();if(m.TryGetProperty("files",out var files)&&files.ValueKind==JsonValueKind.Array)foreach(var f in files.EnumerateArray()){if(f.TryGetProperty("name",out var fn)&&fn.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(fn.GetString()))refs.Add("file:"+fn.GetString());if(f.TryGetProperty("permalink",out var p)&&p.ValueKind==JsonValueKind.String)refs.Add(p.GetString()!);else if(f.TryGetProperty("url_private",out var up)&&up.ValueKind==JsonValueKind.String)refs.Add(up.GetString()!);}var progress=Progress(text);var status=Status(text);result.Add(new("Slack",$"{channel}:{ts}","message",FirstLine(text),text,status,progress,author,null,null,at,ts,null,refs.Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray(),new Dictionary<string,string>{{"Channel",channel},{"Author",author??""}}));}
                cursor=doc.RootElement.TryGetProperty("response_metadata",out var rm)&&rm.TryGetProperty("next_cursor",out var nc)?nc.GetString():null;if(string.IsNullOrWhiteSpace(cursor))break;
            }
        }
        return result;
    }
    private static bool Relevant(string text,JsonElement message)
    {
        if(Regex.IsMatch(text,@"\b(?:DIP|TASK|REDMINE|RM)[:#-][A-Za-z0-9_.-]+|https?://|\d{1,3}(?:\.\d+)?\s*[%％]|完了|進捗|対応|実装|レビュー|テスト|CI|PR|blocked|blocker|done|complete|progress",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
        return message.TryGetProperty("files",out var files)&&files.ValueKind==JsonValueKind.Array&&files.GetArrayLength()>0;
    }
    private static DateTimeOffset ParseSlackTs(string ts){var p=ts.Split('.',2);return long.TryParse(p[0],out var sec)?DateTimeOffset.FromUnixTimeSeconds(sec):DateTimeOffset.UtcNow;}private static IEnumerable<string> Extract(string text)=>Regex.Matches(text,@"https?://[^\s<>\]\)]+|\b(?:DIP|TASK|REDMINE|RM)[:#-][A-Za-z0-9_.-]+",RegexOptions.IgnoreCase).Select(x=>x.Value.TrimEnd('.',',',';'));private static decimal? Progress(string text){var m=Regex.Match(text,@"(?<p>\d{1,3}(?:\.\d+)?)\s*[%％]");return m.Success&&decimal.TryParse(m.Groups["p"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var p)?Math.Clamp(p,0m,100m):null;}private static string Status(string text)=>Regex.IsMatch(text,@"\b(done|complete|completed|finished|完了|終了)\b",RegexOptions.IgnoreCase)?"ClaimedCompleted":Regex.IsMatch(text,@"\b(blocked|blocker|ブロック|待ち)\b",RegexOptions.IgnoreCase)?"Blocked":"ProgressReported";private static string FirstLine(string text){var s=text.Replace("\r","").Split('\n',2)[0].Trim();return s[..Math.Min(300,s.Length)];}public void Dispose()=>_http.Dispose();
}
