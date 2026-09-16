// Purpose: Represents I de nt it yG ov er na nc eE nt it ie s in the Identity domain.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class IdentityProfile
{
    private IdentityProfile() { }
    public IdentityProfile(Guid id,Guid organizationId,IdentityKind kind,string displayName,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;Kind=kind;DisplayName=Req(displayName,240);CreatedAtUtc=now;}
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public IdentityKind Kind{get;private set;} public string DisplayName{get;private set;}=""; public DateTimeOffset CreatedAtUtc{get;private set;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
public sealed class ExternalIdentityLink
{
    private ExternalIdentityLink() { }
    public ExternalIdentityLink(Guid id,Guid identityProfileId,string provider,string externalId,decimal confidence,bool approved,string? approvedBy,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;IdentityProfileId=identityProfileId;Provider=Req(provider,120);ExternalId=Req(externalId,300);Confidence=Math.Clamp(confidence,0m,100m);Approved=approved;ApprovedBy=approvedBy;CreatedAtUtc=now;}
    public Guid Id{get;private set;} public Guid IdentityProfileId{get;private set;} public string Provider{get;private set;}=""; public string ExternalId{get;private set;}=""; public decimal Confidence{get;private set;} public bool Approved{get;private set;} public string? ApprovedBy{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;}
    public void Approve(string actor){Approved=true;ApprovedBy=Req(actor,240);} private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
public sealed class OrganizationRoleBinding
{
    private OrganizationRoleBinding() { }
    public OrganizationRoleBinding(Guid id,Guid organizationId,string subjectId,OrganizationRole role,Guid? projectId,string grantedBy,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;SubjectId=Req(subjectId,300);Role=role;ProjectId=projectId;GrantedBy=Req(grantedBy,240);GrantedAtUtc=now;}
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public string SubjectId{get;private set;}=""; public OrganizationRole Role{get;private set;} public Guid? ProjectId{get;private set;} public string GrantedBy{get;private set;}=""; public DateTimeOffset GrantedAtUtc{get;private set;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
public sealed class RetentionExecutionRecord
{
    private RetentionExecutionRecord() { }
    public RetentionExecutionRecord(Guid id,Guid organizationId,Guid? projectId,string resourceType,RetentionAction action,int affectedCount,string policySnapshotJson,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;ProjectId=projectId;ResourceType=Req(resourceType,120);Action=action;AffectedCount=Math.Max(0,affectedCount);PolicySnapshotJson=Req(policySnapshotJson,32000);ExecutedAtUtc=now;}
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public Guid? ProjectId{get;private set;} public string ResourceType{get;private set;}=""; public RetentionAction Action{get;private set;} public int AffectedCount{get;private set;} public string PolicySnapshotJson{get;private set;}=""; public DateTimeOffset ExecutedAtUtc{get;private set;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
