// Purpose: Represents A rt if ac tR ec or d in the Artifacts domain.
namespace Dip.Domain.Entities;

public sealed class ArtifactRecord
{
    private ArtifactRecord() { }

    public ArtifactRecord(Guid id,Guid projectId,Guid artifactBlobId,string kind,string fileName,string retentionClass,
        Guid? jobId,Guid? qaRunId,Guid? incidentId,Guid? benchmarkRunId,DateTimeOffset createdAtUtc,DateTimeOffset? expiresAtUtc,Guid? gameReplayRunId=null)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        if(artifactBlobId==Guid.Empty)throw new ArgumentException("ArtifactBlobId is required.",nameof(artifactBlobId));
        Id=id==Guid.Empty?Guid.NewGuid():id;ProjectId=projectId;ArtifactBlobId=artifactBlobId;
        Kind=Required(kind,120,nameof(kind));FileName=Required(fileName,500,nameof(fileName));RetentionClass=Required(retentionClass,40,nameof(retentionClass));
        JobId=jobId;QaRunId=qaRunId;IncidentId=incidentId;BenchmarkRunId=benchmarkRunId;GameReplayRunId=gameReplayRunId;CreatedAtUtc=createdAtUtc;ExpiresAtUtc=expiresAtUtc;
    }
    public Guid Id{get;private set;} public Guid ProjectId{get;private set;} public Guid ArtifactBlobId{get;private set;}
    public string Kind{get;private set;}=string.Empty; public string FileName{get;private set;}=string.Empty; public string RetentionClass{get;private set;}=string.Empty;
    public Guid? JobId{get;private set;} public Guid? QaRunId{get;private set;} public Guid? IncidentId{get;private set;} public Guid? BenchmarkRunId{get;private set;} public Guid? GameReplayRunId{get;private set;}
    public DateTimeOffset CreatedAtUtc{get;private set;} public DateTimeOffset? ExpiresAtUtc{get;private set;}
    public void LinkIncident(Guid incidentId){if(incidentId==Guid.Empty)throw new ArgumentException("IncidentId is required.",nameof(incidentId));IncidentId=incidentId;}
    private static string Required(string? value,int max,string name){var text=value?.Trim()??string.Empty;if(text.Length is <1||text.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);return text;}
}
