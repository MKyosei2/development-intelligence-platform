// Purpose: Defines AgentRelease.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class AgentRelease
{
    private AgentRelease() { }
    public AgentRelease(Guid id,Guid organizationId,string version,AgentReleaseChannel channel,string packageUri,string sha256,string signature,string minimumServerVersion,int rolloutPercent,DateTimeOffset now)
    { if(organizationId==Guid.Empty)throw new ArgumentException("OrganizationId is required.");
        Id=id==Guid.Empty?Guid.NewGuid():id;
        OrganizationId=organizationId;
        Version=Req(version,80);
        Channel=channel;
        PackageUri=Req(packageUri,1000);
        Sha256=Req(sha256,128);
        Signature=Req(signature,8000);
        MinimumServerVersion=Req(minimumServerVersion,80);
        RolloutPercent=Math.Clamp(rolloutPercent,0,100);
        State=AgentRolloutState.Draft;
        CreatedAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid OrganizationId{get;
        private set;
        }
        public string Version{get;
        private set;
        }="";
        public AgentReleaseChannel Channel{get;
        private set;
        }
        public string PackageUri{get;
        private set;
        }="";
        public string Sha256{get;
        private set;
        }="";
        public string Signature{get;
        private set;
        }="";
        public string MinimumServerVersion{get;
        private set;
        }="";
        public int RolloutPercent{get;
        private set;
        }
        public AgentRolloutState State{get;
        private set;
        }
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        public DateTimeOffset? ActivatedAtUtc{get;
        private set;
        }
    public void Activate(int percent,DateTimeOffset now){RolloutPercent=Math.Clamp(percent,1,100);
        State=AgentRolloutState.Active;
        ActivatedAtUtc=now;
        }
        public void Pause(){if(State==AgentRolloutState.Active)State=AgentRolloutState.Paused;
        }
        public void Rollback(){State=AgentRolloutState.RolledBack;
        }
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
