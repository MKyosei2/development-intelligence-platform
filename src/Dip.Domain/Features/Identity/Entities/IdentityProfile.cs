// Purpose: Defines the IdentityProfile application contract.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class IdentityProfile
{
    private IdentityProfile() { }
    public IdentityProfile(Guid id,Guid organizationId,IdentityKind kind,string displayName,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;Kind=kind;DisplayName=Req(displayName,240);CreatedAtUtc=now;}
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public IdentityKind Kind{get;private set;} public string DisplayName{get;private set;}=""; public DateTimeOffset CreatedAtUtc{get;private set;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
