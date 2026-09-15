using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class AgentEnrollmentToken
{
    private AgentEnrollmentToken() { }
    public AgentEnrollmentToken(Guid id,Guid organizationId,Guid projectId,string tokenHash,DateTimeOffset expiresAtUtc,DateTimeOffset now)
    { if(organizationId==Guid.Empty||projectId==Guid.Empty)throw new ArgumentException("OrganizationId and ProjectId are required.");Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;ProjectId=projectId;TokenHash=Req(tokenHash,128);ExpiresAtUtc=expiresAtUtc;CreatedAtUtc=now; }
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public Guid ProjectId{get;private set;} public string TokenHash{get;private set;}=""; public DateTimeOffset ExpiresAtUtc{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;} public DateTimeOffset? ConsumedAtUtc{get;private set;} public bool IsConsumed=>ConsumedAtUtc.HasValue;
    public void Consume(DateTimeOffset now){if(IsConsumed)throw new InvalidOperationException("Enrollment token already consumed.");ConsumedAtUtc=now;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}

public sealed class AgentRegistration
{
    private AgentRegistration() { }
    public AgentRegistration(Guid id,Guid organizationId,Guid projectId,string agentId,string machineId,string publicKeyPem,string version,int policyVersion,DateTimeOffset now)
    { if(organizationId==Guid.Empty||projectId==Guid.Empty)throw new ArgumentException("OrganizationId and ProjectId are required.");Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;ProjectId=projectId;AgentId=Req(agentId,160);MachineId=Req(machineId,160);PublicKeyPem=Req(publicKeyPem,12000);Version=Req(version,80);PolicyVersion=policyVersion;Status=AgentFleetStatus.Active;EnrolledAtUtc=now;LastSeenAtUtc=now; }
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public Guid ProjectId{get;private set;} public string AgentId{get;private set;}=""; public string MachineId{get;private set;}=""; public string PublicKeyPem{get;private set;}=""; public string Version{get;private set;}=""; public int PolicyVersion{get;private set;} public AgentFleetStatus Status{get;private set;} public DateTimeOffset EnrolledAtUtc{get;private set;} public DateTimeOffset LastSeenAtUtc{get;private set;} public string? HealthJson{get;private set;}
    public void Heartbeat(string version,int policyVersion,string? healthJson,DateTimeOffset now){Version=Req(version,80);PolicyVersion=Math.Max(0,policyVersion);HealthJson=Clean(healthJson,32000);LastSeenAtUtc=now;if(Status!=AgentFleetStatus.Revoked)Status=AgentFleetStatus.Active;}
    public void Revoke(DateTimeOffset now){Status=AgentFleetStatus.Revoked;LastSeenAtUtc=now;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;} private static string? Clean(string? v,int m){if(string.IsNullOrWhiteSpace(v))return null;var x=v.Trim();if(x.Length>m)throw new ArgumentException("Value too long.");return x;}
}

public sealed class AgentRelease
{
    private AgentRelease() { }
    public AgentRelease(Guid id,Guid organizationId,string version,AgentReleaseChannel channel,string packageUri,string sha256,string signature,string minimumServerVersion,int rolloutPercent,DateTimeOffset now)
    { if(organizationId==Guid.Empty)throw new ArgumentException("OrganizationId is required.");Id=id==Guid.Empty?Guid.NewGuid():id;OrganizationId=organizationId;Version=Req(version,80);Channel=channel;PackageUri=Req(packageUri,1000);Sha256=Req(sha256,128);Signature=Req(signature,8000);MinimumServerVersion=Req(minimumServerVersion,80);RolloutPercent=Math.Clamp(rolloutPercent,0,100);State=AgentRolloutState.Draft;CreatedAtUtc=now; }
    public Guid Id{get;private set;} public Guid OrganizationId{get;private set;} public string Version{get;private set;}=""; public AgentReleaseChannel Channel{get;private set;} public string PackageUri{get;private set;}=""; public string Sha256{get;private set;}=""; public string Signature{get;private set;}=""; public string MinimumServerVersion{get;private set;}=""; public int RolloutPercent{get;private set;} public AgentRolloutState State{get;private set;} public DateTimeOffset CreatedAtUtc{get;private set;} public DateTimeOffset? ActivatedAtUtc{get;private set;}
    public void Activate(int percent,DateTimeOffset now){RolloutPercent=Math.Clamp(percent,1,100);State=AgentRolloutState.Active;ActivatedAtUtc=now;} public void Pause(){if(State==AgentRolloutState.Active)State=AgentRolloutState.Paused;} public void Rollback(){State=AgentRolloutState.RolledBack;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
