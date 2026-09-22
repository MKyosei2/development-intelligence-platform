// Purpose: Defines AgentEnrollmentToken.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class AgentEnrollmentToken
{
    private AgentEnrollmentToken() { }
    public AgentEnrollmentToken(Guid id,Guid organizationId,Guid projectId,string tokenHash,DateTimeOffset expiresAtUtc,DateTimeOffset now)
    { if(organizationId==Guid.Empty||projectId==Guid.Empty)throw new ArgumentException("OrganizationId and ProjectId are required.");
        Id=id==Guid.Empty?Guid.NewGuid():id;
        OrganizationId=organizationId;
        ProjectId=projectId;
        TokenHash=Req(tokenHash,128);
        ExpiresAtUtc=expiresAtUtc;
        CreatedAtUtc=now;
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
        public string TokenHash{get;
        private set;
        }="";
        public DateTimeOffset ExpiresAtUtc{get;
        private set;
        }
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        public DateTimeOffset? ConsumedAtUtc{get;
        private set;
        }
        public bool IsConsumed=>ConsumedAtUtc.HasValue;
    public void Consume(DateTimeOffset now){if(IsConsumed)throw new InvalidOperationException("Enrollment token already consumed.");ConsumedAtUtc=now;}
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
