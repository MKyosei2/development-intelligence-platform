// Purpose: Defines RetentionExecutionRecord.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class RetentionExecutionRecord
{
    private RetentionExecutionRecord() { }
    public RetentionExecutionRecord(Guid id,Guid organizationId,Guid? projectId,string resourceType,RetentionAction action,int affectedCount,string policySnapshotJson,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        OrganizationId=organizationId;
        ProjectId=projectId;
        ResourceType=Req(resourceType,120);
        Action=action;
        AffectedCount=Math.Max(0,affectedCount);
        PolicySnapshotJson=Req(policySnapshotJson,32000);
        ExecutedAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid OrganizationId{get;
        private set;
        }
        public Guid? ProjectId{get;
        private set;
        }
        public string ResourceType{get;
        private set;
        }="";
        public RetentionAction Action{get;
        private set;
        }
        public int AffectedCount{get;
        private set;
        }
        public string PolicySnapshotJson{get;
        private set;
        }="";
        public DateTimeOffset ExecutedAtUtc{get;
        private set;
        }
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
