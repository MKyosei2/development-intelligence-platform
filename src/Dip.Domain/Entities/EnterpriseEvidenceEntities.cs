using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class EvidenceIntegrityReceipt
{
    private EvidenceIntegrityReceipt() { }
    public EvidenceIntegrityReceipt(Guid id,Guid organizationId,Guid projectId,Guid evidenceId,string payloadHash,string previousHash,string chainHash,string? sourceSignature,string? sourceKeyId,EvidenceChainStatus status,DateTimeOffset ingestedAtUtc)
    { Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;ProjectId=projectId;EvidenceId=evidenceId;PayloadHash=Req(payloadHash,128);PreviousHash=Req(previousHash,128);ChainHash=Req(chainHash,128);SourceSignature=Clean(sourceSignature,16000);SourceKeyId=Clean(sourceKeyId,240);Status=status;IngestedAtUtc=ingestedAtUtc; }
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public Guid ProjectId{get;private set;} public Guid EvidenceId{get;private set;} public string PayloadHash{get;private set;}=""; public string PreviousHash{get;private set;}=""; public string ChainHash{get;private set;}=""; public string? SourceSignature{get;private set;} public string? SourceKeyId{get;private set;} public EvidenceChainStatus Status{get;private set;} public DateTimeOffset IngestedAtUtc{get;private set;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid hash.");return x;} private static string? Clean(string? v,int m){if(string.IsNullOrWhiteSpace(v))return null;var x=v.Trim();if(x.Length>m)throw new ArgumentException("Value too long.");return x;}
}

public sealed class DistributedIngestionEvent
{
    private DistributedIngestionEvent() { }
    public DistributedIngestionEvent(Guid id,Guid organizationId,Guid projectId,string idempotencyKey,long sourceSequence,string envelopeJson,DateTimeOffset observedAtUtc,DateTimeOffset now)
    { Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;ProjectId=projectId;IdempotencyKey=Req(idempotencyKey,300);SourceSequence=sourceSequence;EnvelopeJson=Req(envelopeJson,128000);ObservedAtUtc=observedAtUtc;ReceivedAtUtc=now;State=DistributedIngestionState.Pending; }
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public Guid ProjectId{get;private set;} public string IdempotencyKey{get;private set;}=""; public long SourceSequence{get;private set;} public string EnvelopeJson{get;private set;}=""; public DateTimeOffset ObservedAtUtc{get;private set;} public DateTimeOffset ReceivedAtUtc{get;private set;} public DistributedIngestionState State{get;private set;} public int AttemptCount{get;private set;} public string? LastError{get;private set;}
    public void Start(){State=DistributedIngestionState.Processing;AttemptCount++;} public void Complete(){State=DistributedIngestionState.Completed;LastError=null;} public void Fail(string error){LastError=(error??"").Trim();State=AttemptCount>=5?DistributedIngestionState.DeadLetter:DistributedIngestionState.Failed;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
