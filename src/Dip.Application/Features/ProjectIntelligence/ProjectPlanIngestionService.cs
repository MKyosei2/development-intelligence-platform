// Purpose: Orchestrates the DIP workflow for P ro je ct Pl an In ge st io nS er vi ce.
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Management;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectIntelligence;

public sealed class ProjectPlanIngestionService
{
    public const long MaxUploadBytes=12*1024*1024;
    private readonly ProjectPlanUnderstandingEngine _engine;
    private readonly DevelopmentGraphService _graph;
    private readonly ProjectEntityResolutionService _resolver;
    private readonly ISystemClock _clock;

    public ProjectPlanIngestionService(ProjectPlanUnderstandingEngine engine,DevelopmentGraphService graph,ProjectEntityResolutionService resolver,ISystemClock clock)
    { _engine=engine;_graph=graph;_resolver=resolver;_clock=clock; }

    public async Task<ProjectPlanAnalysisDto> AnalyzeAsync(string fileName,Stream stream,CancellationToken ct)
    {
        var (bytes,tables)=await ReadTablesAsync(fileName,stream,ct);
        var analysis=_engine.Analyze(fileName,tables,_clock.UtcNow);
        return analysis with{SourceSha256=Sha(bytes)};
    }

    public async Task<ProjectPlanImportResult> ImportAsync(Guid projectId,string fileName,Stream stream,IReadOnlyDictionary<string,PlanColumnMappingDto>? mappings,CancellationToken ct)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var (bytes,tables)=await ReadTablesAsync(fileName,stream,ct);var sha=Sha(bytes);var warnings=new List<string>();
        var items=_engine.ParseAll(tables,mappings,warnings).Take(20000).ToArray();
        if(items.Length==0)throw new InvalidDataException("No WBS/Gantt rows were recognized. Analyze the workbook and confirm the detected column mapping before importing.");
        if(items.Length>=20000)warnings.Add("Import was capped at 20,000 plan rows for one workbook revision.");
        var importKey=$"{Safe(Path.GetFileNameWithoutExtension(fileName),80)}-{sha[..12]}";
        var import=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.ProjectPlanImport,$"plan-import:{importKey}",Path.GetFileName(fileName),"Active","Excel/WBS",null,
            JsonSerializer.Serialize(new{ImportKey=importKey,FileName=Path.GetFileName(fileName),SourceSha256=sha,ImportedAtUtc=_clock.UtcNow,Items=items.Length,Source="uploaded-workbook"})),ct);
        var map=new Dictionary<string,DevelopmentGraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in items)
        {
            ct.ThrowIfCancellationRequested();
            var key=$"plan:{importKey}:{Safe(item.Key,160)}";
            var metadata=JsonSerializer.Serialize(new{item.Key,item.ParentKey,item.PlannedStartUtc,item.DueAtUtc,item.DeclaredProgressPercent,item.Owner,item.Status,item.DependencyKeys,item.IsMilestone,item.SheetName,item.SourceRow,item.SourceReference,ImportKey=importKey,SourceSha256=sha});
            var node=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.PlanItem,key,item.Name,item.Status??"Planned","Excel/WBS",$"{Path.GetFileName(fileName)}#{item.SourceReference}",metadata),ct);
            map[item.Key]=node;await _graph.LinkAsync(new(projectId,import.Id,node.Id,DevelopmentGraphEdgeType.Contains,100m,false,"{\"sourceProvenance\":true}"),ct);
        }
        var parentLinks=0;var depLinks=0;
        foreach(var item in items)
        {
            if(!map.TryGetValue(item.Key,out var node))continue;
            if(!string.IsNullOrWhiteSpace(item.ParentKey)&&map.TryGetValue(item.ParentKey,out var parent))
            {await _graph.LinkAsync(new(projectId,parent.Id,node.Id,DevelopmentGraphEdgeType.Contains,85m,false,"{\"wbsHierarchy\":true}"),ct);parentLinks++;}
            foreach(var dep in item.DependencyKeys)
            {
                if(!map.TryGetValue(dep,out var predecessor))continue;
                await _graph.LinkAsync(new(projectId,predecessor.Id,node.Id,DevelopmentGraphEdgeType.DependsOn,90m,true,"{\"planDependency\":true}"),ct);depLinks++;
            }
        }
        var beforeResolution=await _resolver.ResolveProjectAsync(projectId,false,ct);
        var resolved=await _resolver.ResolveProjectAsync(projectId,true,ct);
        var newlyResolved=Math.Max(0,resolved.Resolved-beforeResolution.Resolved);
        return new(projectId,importKey,Path.GetFileName(fileName),sha,items.Length,parentLinks,depLinks,newlyResolved,warnings.Take(100).ToArray(),_clock.UtcNow);
    }

    public static IReadOnlyDictionary<string,PlanColumnMappingDto>? ParseMappingJson(string? json)
    {
        if(string.IsNullOrWhiteSpace(json))return null;
        var items=JsonSerializer.Deserialize<PlanColumnMappingDto[]>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
        return items.GroupBy(x=>x.SheetName,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last(),StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<(byte[] Bytes,IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<string>>> Tables)> ReadTablesAsync(string fileName,Stream stream,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(fileName))throw new ArgumentException("File name is required.",nameof(fileName));
        using var ms=new MemoryStream();var buffer=new byte[81920];long total=0;
        while(true){var read=await stream.ReadAsync(buffer,ct);if(read==0)break;total+=read;if(total>MaxUploadBytes)throw new InvalidDataException($"Plan file exceeds the {MaxUploadBytes/1024/1024} MB upload limit.");await ms.WriteAsync(buffer.AsMemory(0,read),ct);}var bytes=ms.ToArray();if(bytes.Length==0)throw new InvalidDataException("Plan file is empty.");
        var ext=Path.GetExtension(fileName).ToLowerInvariant();
        if(ext==".csv")return(bytes,new Dictionary<string,IReadOnlyList<IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase){{"CSV",ReadCsv(Encoding.UTF8.GetString(bytes))}});
        if(ext is not ".xlsx" and not ".xlsm")throw new InvalidDataException("Only .xlsx, .xlsm and .csv plan files are supported. Legacy .xls must be saved as .xlsx or CSV first.");
        ValidateZip(bytes);using var input=new MemoryStream(bytes,false);return(bytes,ManagementXlsxCodec.Read(input));
    }

    private static void ValidateZip(byte[] bytes)
    {
        using var ms=new MemoryStream(bytes,false);using var zip=new ZipArchive(ms,ZipArchiveMode.Read,true);if(zip.Entries.Count>2000)throw new InvalidDataException("Workbook contains too many package entries.");long expanded=0;
        foreach(var e in zip.Entries){expanded+=e.Length;if(e.Length>32*1024*1024||expanded>80*1024*1024)throw new InvalidDataException("Workbook expanded size exceeds the safe parsing limit.");}
    }
    private static IReadOnlyList<IReadOnlyList<string>> ReadCsv(string text)
    {
        var rows=new List<IReadOnlyList<string>>();var row=new List<string>();var cell=new StringBuilder();var quoted=false;
        for(var i=0;i<text.Length;i++)
        {
            var c=text[i];if(quoted){if(c=='"'&&i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else if(c=='"')quoted=false;else cell.Append(c);continue;}
            if(c=='"'){quoted=true;continue;}if(c==','){row.Add(cell.ToString());cell.Clear();continue;}if(c=='\r')continue;if(c=='\n'){row.Add(cell.ToString());cell.Clear();rows.Add(row.ToArray());row=[];continue;}cell.Append(c);
        }
        if(cell.Length>0||row.Count>0){row.Add(cell.ToString());rows.Add(row.ToArray());}if(rows.Count>0&&rows[0].Count>0)rows[0]=rows[0].Select((x,i)=>i==0?x.TrimStart('\uFEFF'):x).ToArray();return rows;
    }
    private static string Sha(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Safe(string? value,int max){var v=(value??"item").Trim();foreach(var c in Path.GetInvalidFileNameChars())v=v.Replace(c,'-');v=v.Replace(':','-').Replace('/','-').Replace('\\','-');return string.IsNullOrWhiteSpace(v)?"item":v[..Math.Min(max,v.Length)];}
}
