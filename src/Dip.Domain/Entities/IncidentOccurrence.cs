namespace Dip.Domain.Entities;
public sealed class IncidentOccurrence
{
    private IncidentOccurrence() { }
    public IncidentOccurrence(Guid id,Guid incidentId,Guid projectId,string subjectKey,DateTimeOffset observedAtUtc,Guid? qaFindingId=null,Guid? qaRunId=null,Guid? jobId=null,string? commitSha=null,string? detailJson=null)
    {
        if(incidentId==Guid.Empty||projectId==Guid.Empty)throw new ArgumentException("IncidentId and ProjectId are required.");
        Id=id==Guid.Empty?Guid.NewGuid():id;IncidentId=incidentId;ProjectId=projectId;SubjectKey=Required(subjectKey,200,nameof(subjectKey));ObservedAtUtc=observedAtUtc;QaFindingId=qaFindingId;QaRunId=qaRunId;JobId=jobId;
        CommitSha=Trim(commitSha,64);DetailJson=Trim(detailJson,16000);
    }
    public Guid Id{get;private set;} public Guid IncidentId{get;private set;} public Guid ProjectId{get;private set;} public string SubjectKey{get;private set;}=string.Empty;
    public Guid? QaFindingId{get;private set;} public Guid? QaRunId{get;private set;} public Guid? JobId{get;private set;} public string? CommitSha{get;private set;} public string? DetailJson{get;private set;} public DateTimeOffset ObservedAtUtc{get;private set;}
    private static string Required(string? value,int max,string name){var text=value?.Trim()??string.Empty;if(text.Length is <1||text.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);return text;}
    private static string? Trim(string? value,int max)=>string.IsNullOrWhiteSpace(value)?null:(value.Trim().Length<=max?value.Trim():value.Trim()[..max]);
}
