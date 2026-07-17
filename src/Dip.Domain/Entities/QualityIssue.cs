using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class QualityIssue
{
    private QualityIssue(){}
    public QualityIssue(Guid id,Guid projectId,Guid runId,string fingerprint,string category,Severity severity,
        string title,string description,string? source,string? ruleId,string? evidenceJson,DateTimeOffset created)
    {
        Id=id==Guid.Empty?Guid.NewGuid():id; ProjectId=projectId; PipelineRunId=runId;
        FirstSeenPipelineRunId=runId; LastSeenPipelineRunId=runId; Fingerprint=Req(fingerprint,64);
        Category=Req(category,80); Severity=severity; Title=Req(title,200); Description=Req(description,4000);
        Source=Opt(source,1000); RuleId=Opt(ruleId,200); EvidenceJson=Opt(evidenceJson,16000);
        Status=IssueStatus.Open; OccurrenceCount=1; CreatedAtUtc=LastSeenAtUtc=created;
    }

    public Guid Id{get;private set;}
    public Guid ProjectId{get;private set;}
    public Guid PipelineRunId{get;private set;}
    public string Fingerprint{get;private set;}=string.Empty;
    public string Category{get;private set;}=string.Empty;
    public Severity Severity{get;private set;}
    public string Title{get;private set;}=string.Empty;
    public string Description{get;private set;}=string.Empty;
    public string? Source{get;private set;}
    public string? RuleId{get;private set;}
    public string? EvidenceJson{get;private set;}
    public IssueStatus Status{get;private set;}
    public int OccurrenceCount{get;private set;}
    public Guid FirstSeenPipelineRunId{get;private set;}
    public Guid LastSeenPipelineRunId{get;private set;}
    public DateTimeOffset CreatedAtUtc{get;private set;}
    public DateTimeOffset LastSeenAtUtc{get;private set;}
    public DateTimeOffset? ResolvedAtUtc{get;private set;}

    public void Reoccur(Guid runId,Severity severity,DateTimeOffset at)
    {
        PipelineRunId=LastSeenPipelineRunId=runId;LastSeenAtUtc=at;OccurrenceCount++;
        if(severity>Severity)Severity=severity;
        if(Status is IssueStatus.Resolved or IssueStatus.Ignored or IssueStatus.FalsePositive)Status=IssueStatus.Reopened;
        ResolvedAtUtc=null;
    }
    public void Resolve(DateTimeOffset at){if(Status==IssueStatus.Resolved)return;Status=IssueStatus.Resolved;ResolvedAtUtc=at;}
    private static string Req(string? v,int max){var s=v?.Trim()??"";if(s.Length is <1||s.Length>max)throw new ArgumentException();return s;}
    private static string? Opt(string? v,int max){if(string.IsNullOrWhiteSpace(v))return null;var s=v.Trim();if(s.Length>max)throw new ArgumentException();return s;}
}
