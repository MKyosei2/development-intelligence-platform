// Purpose: Integrates external or runtime capabilities through S en tr yI nc ID en tI ng es ti on Ad ap te r.
using System.Text.Json;
using Dip.Application.Features.ExternalEvents;

namespace Dip.Infrastructure.ExternalEvents;

public sealed class SentryIncidentIngestionAdapter:IExternalIncidentIngestionAdapter
{
    public string Provider=>"sentry";
    public bool CanHandle(string provider)=>provider.Equals("sentry",StringComparison.OrdinalIgnoreCase);
    public ExternalIncidentEnvelope Parse(JsonElement p)
    {
        var eventId=String(p,"event_id")??String(p,"id")??Guid.NewGuid().ToString("N");
        var title=String(p,"title")??String(p,"message")??"Sentry runtime event";var message=String(p,"message")??title;string? type=null,stack=null,file=null;int? line=null;
        if(p.TryGetProperty("exception",out var ex)&&ex.TryGetProperty("values",out var values)&&values.ValueKind==JsonValueKind.Array&&values.GetArrayLength()>0)
        {
            var last=values[values.GetArrayLength()-1];type=String(last,"type");message=String(last,"value")??message;
            if(last.TryGetProperty("stacktrace",out var st)&&st.TryGetProperty("frames",out var frames)&&frames.ValueKind==JsonValueKind.Array)
            {
                var list=frames.EnumerateArray().Select(f=>$"{String(f,"function")??"?"} at {String(f,"filename")??String(f,"abs_path")??"?"}:{Int(f,"lineno")?.ToString()??"?"}").ToArray();stack=string.Join(Environment.NewLine,list);
                var frame=frames.EnumerateArray().LastOrDefault();if(frame.ValueKind==JsonValueKind.Object){file=String(frame,"filename")??String(frame,"abs_path");line=Int(frame,"lineno");}
            }
        }
        var tags=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);if(p.TryGetProperty("tags",out var t)){if(t.ValueKind==JsonValueKind.Object)foreach(var x in t.EnumerateObject())tags[x.Name]=x.Value.ToString();else if(t.ValueKind==JsonValueKind.Array)foreach(var x in t.EnumerateArray())if(x.ValueKind==JsonValueKind.Array&&x.GetArrayLength()>=2)tags[x[0].ToString()]=x[1].ToString();}
        var route=String(p,"transaction")??tags.GetValueOrDefault("scene")??tags.GetValueOrDefault("route");var observed=DateTimeOffset.UtcNow;if(DateTimeOffset.TryParse(String(p,"timestamp"),out var parsed))observed=parsed;
        return new(eventId,title,message,type,stack,file,line,route,tags,observed);
    }
    private static string? String(JsonElement e,string n)=>e.TryGetProperty(n,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static int? Int(JsonElement e,string n)=>e.TryGetProperty(n,out var v)&&v.TryGetInt32(out var i)?i:null;
}
