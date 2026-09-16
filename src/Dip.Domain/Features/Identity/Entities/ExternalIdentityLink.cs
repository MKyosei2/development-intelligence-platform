// Purpose: Defines ExternalIdentityLink.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class ExternalIdentityLink
{
    private ExternalIdentityLink() { }
    public ExternalIdentityLink(Guid id,Guid identityProfileId,string provider,string externalId,decimal confidence,bool approved,string? approvedBy,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;IdentityProfileId=identityProfileId;Provider=Req(provider,120);ExternalId=Req(externalId,300);Confidence=Math.Clamp(confidence,0m,100m);Approved=approved;ApprovedBy=approvedBy;CreatedAtUtc=now;}
    public Guid Id{get;private set;} public Guid IdentityProfileId{get;private set;} public string Provider{get;private set;}=""; public string ExternalId{get;private set;}=""; public decimal Confidence{get;private set;} public bool Approved{get;private set;} public string? ApprovedBy{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;}
    public void Approve(string actor){Approved=true;ApprovedBy=Req(actor,240);} private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
