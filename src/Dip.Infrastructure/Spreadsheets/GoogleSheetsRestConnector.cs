// Purpose: Integrates external or runtime capabilities through G oo gl eS he et sR es tC on ne ct or.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Application.Abstractions.Spreadsheets;

namespace Dip.Infrastructure.Spreadsheets;

public sealed class GoogleSheetsRestConnector:IGoogleSheetsConnector
{
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(45)};
    private const string Base="https://sheets.googleapis.com/v4/spreadsheets";

    public async Task EnsureSheetsAsync(string spreadsheetId,string accessToken,IReadOnlyList<string> sheetNames,CancellationToken ct)
    {
        Validate(spreadsheetId,accessToken);
            using var req=Create(HttpMethod.Get,$"{Base}/{Uri.EscapeDataString(spreadsheetId)}?fields=sheets.properties.title",accessToken);
            using var response=await Http.SendAsync(req,ct);
            await Ensure(response,ct);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(doc.RootElement.TryGetProperty("sheets",out var sheets)){foreach(var s in sheets.EnumerateArray()){if(s.TryGetProperty("properties",out var p)&&p.TryGetProperty("title",out var t)&&t.GetString() is {Length:>0} title)existing.Add(title);
            }}
        var missing=sheetNames.Where(x=>!existing.Contains(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(missing.Length==0)return;
            var body=new{requests=missing.Select(x=>new{addSheet=new{properties=new{title=x}}}).ToArray()};
            using var add=Create(HttpMethod.Post,$"{Base}/{Uri.EscapeDataString(spreadsheetId)}:batchUpdate",accessToken);
            add.Content=JsonContent.Create(body);
            using var added=await Http.SendAsync(add,ct);
            await Ensure(added,ct);
    }

    public async Task PushAsync(string spreadsheetId,string accessToken,IReadOnlyList<GoogleSheetGrid> sheets,CancellationToken ct)
    {
        Validate(spreadsheetId,accessToken);await EnsureSheetsAsync(spreadsheetId,accessToken,sheets.Select(x=>x.Name).ToArray(),ct);
        foreach(var sheet in sheets){var range=$"'{sheet.Name.Replace("'","''",StringComparison.Ordinal)}'!A1";var url=$"{Base}/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString(range)}?valueInputOption=RAW";using var req=Create(HttpMethod.Put,url,accessToken);req.Content=JsonContent.Create(new{range,majorDimension="ROWS",values=sheet.Rows});using var response=await Http.SendAsync(req,ct);await Ensure(response,ct);}
        await FormatSheetsAsync(spreadsheetId,accessToken,sheets.Select(x=>x.Name).ToArray(),ct);
    }

    public async Task<IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<string>>>> PullAsync(string spreadsheetId,string accessToken,IReadOnlyList<string> sheetNames,CancellationToken ct)
    {
        Validate(spreadsheetId,accessToken);var result=new Dictionary<string,IReadOnlyList<IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in sheetNames){var range=$"'{name.Replace("'","''",StringComparison.Ordinal)}'!A:ZZ";var url=$"{Base}/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString(range)}?majorDimension=ROWS";using var req=Create(HttpMethod.Get,url,accessToken);using var response=await Http.SendAsync(req,ct);if(response.StatusCode==System.Net.HttpStatusCode.BadRequest||response.StatusCode==System.Net.HttpStatusCode.NotFound){result[name]=[];continue;}await Ensure(response,ct);using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var rows=new List<IReadOnlyList<string>>();if(doc.RootElement.TryGetProperty("values",out var values)&&values.ValueKind==JsonValueKind.Array){foreach(var row in values.EnumerateArray())rows.Add(row.ValueKind==JsonValueKind.Array?row.EnumerateArray().Select(v=>v.ToString()).ToArray():[]);}result[name]=rows;}
        return result;
    }

    private static async Task FormatSheetsAsync(string spreadsheetId,string accessToken,IReadOnlyList<string> names,CancellationToken ct)
    {
        using var metaReq=Create(HttpMethod.Get,$"{Base}/{Uri.EscapeDataString(spreadsheetId)}?fields=sheets.properties(sheetId,title)",accessToken);
            using var metaResp=await Http.SendAsync(metaReq,ct);
            await Ensure(metaResp,ct);
            using var doc=JsonDocument.Parse(await metaResp.Content.ReadAsStringAsync(ct));
            var ids=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            if(doc.RootElement.TryGetProperty("sheets",out var sheets)){foreach(var s in sheets.EnumerateArray()){if(!s.TryGetProperty("properties",out var p))continue;
            if(p.TryGetProperty("title",out var t)&&p.TryGetProperty("sheetId",out var id)&&id.TryGetInt32(out var i)&&t.GetString() is {Length:>0} title)ids[title]=i;
            }}var requests=new List<object>();
            foreach(var name in names.Distinct(StringComparer.OrdinalIgnoreCase)){if(!ids.TryGetValue(name,out var id))continue;
            requests.Add(new{updateSheetProperties=new{properties=new{sheetId=id,gridProperties=new{frozenRowCount=1}},fields="gridProperties.frozenRowCount"}});
            requests.Add(new{repeatCell=new{range=new{sheetId=id,startRowIndex=0,endRowIndex=1},cell=new{userEnteredFormat=new{backgroundColor=new{red=0.12,green=0.31,blue=0.47},textFormat=new{bold=true,foregroundColor=new{red=1.0,green=1.0,blue=1.0}}}},fields="userEnteredFormat(backgroundColor,textFormat)"}});
            requests.Add(new{autoResizeDimensions=new{dimensions=new{sheetId=id,dimension="COLUMNS",startIndex=0,endIndex=26}}});
            }
            if(requests.Count==0)return;
            using var req=Create(HttpMethod.Post,$"{Base}/{Uri.EscapeDataString(spreadsheetId)}:batchUpdate",accessToken);
            req.Content=JsonContent.Create(new{requests});
            using var resp=await Http.SendAsync(req,ct);
            await Ensure(resp,ct);
    }

    private static HttpRequestMessage Create(HttpMethod method,string url,string token){var req=new HttpRequestMessage(method,url);
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.Trim());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
        }
    private static void Validate(string id,string token){if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("SpreadsheetId is required.",nameof(id));
        if(string.IsNullOrWhiteSpace(token))throw new ArgumentException("A Google OAuth access token is required for this request.",nameof(token));
        }
    private static async Task Ensure(HttpResponseMessage response,CancellationToken ct){if(response.IsSuccessStatusCode)return;
        var body=await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Google Sheets API returned {(int)response.StatusCode}: {body[..Math.Min(body.Length,1200)]}");
        }
}
