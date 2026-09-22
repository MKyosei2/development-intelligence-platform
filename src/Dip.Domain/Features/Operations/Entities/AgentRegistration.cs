// Purpose: Defines AgentRegistration.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class AgentRegistration
{
    private AgentRegistration() { }
    public AgentRegistration(Guid id,Guid organizationId,Guid projectId,string agentId,string machineId,string publicKeyPem,string version,int policyVersion,DateTimeOffset now)
    { if(organizationId==Guid.Empty||projectId==Guid.Empty)throw new ArgumentException("OrganizationId and ProjectId are required.");
        Id=id==Guid.Empty?Guid.NewGuid():id;
        OrganizationId=organizationId;
        ProjectId=projectId;
        AgentId=Req(agentId,160);
        MachineId=Req(machineId,160);
        PublicKeyPem=Req(publicKeyPem,12000);
        Version=Req(version,80);
        PolicyVersion=policyVersion;
        Status=AgentFleetStatus.Active;
        EnrolledAtUtc=now;
        LastSeenAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid OrganizationId{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string AgentId{get;
        private set;
        }="";
        public string MachineId{get;
        private set;
        }="";
        public string PublicKeyPem{get;
        private set;
        }="";
        public string Version{get;
        private set;
        }="";
        public int PolicyVersion{get;
        private set;
        }
        public AgentFleetStatus Status{get;
        private set;
        }
        public DateTimeOffset EnrolledAtUtc{get;
        private set;
        }
        public DateTimeOffset LastSeenAtUtc{get;
        private set;
        }
        public string? HealthJson{get;
        private set;
        }
    public void Heartbeat(string version,int policyVersion,string? healthJson,DateTimeOffset now){Version=Req(version,80);
        PolicyVersion=Math.Max(0,policyVersion);
        HealthJson=Clean(healthJson,32000);
        LastSeenAtUtc=now;
        if(Status!=AgentFleetStatus.Revoked)Status=AgentFleetStatus.Active;
        }
    public void Revoke(DateTimeOffset now){Status=AgentFleetStatus.Revoked;LastSeenAtUtc=now;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";
        if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");
        return x;
        }
        private static string? Clean(string? v,int m){if(string.IsNullOrWhiteSpace(v))return null;
        var x=v.Trim();
        if(x.Length>m)throw new ArgumentException("Value too long.");
        return x;
        }
}
