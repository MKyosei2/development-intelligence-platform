using System.Text.RegularExpressions;
using Dip.Shared.Contracts.Collectors;
namespace Dip.Application.Features.Collectors;

public sealed class CollectorReportValidator
{
    private static readonly Regex Sha=new("^[a-fA-F0-9]{7,64}$",RegexOptions.Compiled);
    public void ValidateAndThrow(CollectorReport r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var e=new List<string>();
        if(r.SchemaVersion!="1.0")e.Add($"Unsupported schema version '{r.SchemaVersion}'.");
        Req(r.ReportId,200,nameof(r.ReportId),e); Req(r.ExternalRunId,200,nameof(r.ExternalRunId),e);
        Req(r.Repository,300,nameof(r.Repository),e); Req(r.Branch,200,nameof(r.Branch),e);
        Req(r.CollectorType,100,nameof(r.CollectorType),e); Req(r.CollectorVersion,50,nameof(r.CollectorVersion),e);
        if(r.ProjectId==Guid.Empty)e.Add("ProjectId must not be empty.");
        if(r.AttemptNumber<1)e.Add("AttemptNumber must be at least 1.");
        if(!Sha.IsMatch(r.CommitSha??""))e.Add("CommitSha must contain 7-64 hexadecimal characters.");
        if(r.FinishedAt<r.StartedAt)e.Add("FinishedAt must not precede StartedAt.");
        if(r.Metrics.Count>10000)e.Add("Metrics must not exceed 10,000.");
        if(r.Issues.Count>10000)e.Add("Issues must not exceed 10,000.");
        var dup=r.Metrics.GroupBy(x=>$"{x.Name.Trim().ToLowerInvariant()}|{x.EnvironmentId?.Trim().ToLowerInvariant()}|{x.ScenarioId?.Trim().ToLowerInvariant()}")
            .Where(x=>x.Count()>1).Select(x=>x.Key).ToArray();
        if(dup.Length>0)e.Add($"Duplicate metric keys: {string.Join(", ",dup)}.");
        foreach(var m in r.Metrics){Req(m.Name,120,"Metric.Name",e);Req(m.Unit,30,"Metric.Unit",e);}
        foreach(var i in r.Issues){Req(i.Category,80,"Issue.Category",e);Req(i.Title,200,"Issue.Title",e);Req(i.Description,4000,"Issue.Description",e);}
        if(e.Count>0)throw new ArgumentException(string.Join(" ",e));
    }
    private static void Req(string? v,int max,string n,ICollection<string> e)
    {var len=v?.Trim().Length??0;if(len is <1||len>max)e.Add($"{n} length must be 1-{max}.");}
}
