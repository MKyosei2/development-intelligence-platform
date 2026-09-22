// Purpose: Defines DistributedIngestionEvent.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class DistributedIngestionEvent
{
    private DistributedIngestionEvent() { }
    public DistributedIngestionEvent(Guid id,Guid organizationId,Guid projectId,string idempotencyKey,long sourceSequence,string envelopeJson,DateTimeOffset observedAtUtc,DateTimeOffset now)
    { Id=id==Guid.Empty?Guid.NewGuid():id;
        OrganizationId=organizationId;
        ProjectId=projectId;
        IdempotencyKey=Req(idempotencyKey,300);
        SourceSequence=sourceSequence;
        EnvelopeJson=Req(envelopeJson,128000);
        ObservedAtUtc=observedAtUtc;
        ReceivedAtUtc=now;
        State=DistributedIngestionState.Pending;
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
        public string IdempotencyKey{get;
        private set;
        }="";
        public long SourceSequence{get;
        private set;
        }
        public string EnvelopeJson{get;
        private set;
        }="";
        public DateTimeOffset ObservedAtUtc{get;
        private set;
        }
        public DateTimeOffset ReceivedAtUtc{get;
        private set;
        }
        public DistributedIngestionState State{get;
        private set;
        }
        public int AttemptCount{get;
        private set;
        }
        public string? LastError{get;
        private set;
        }
    public void Start(){State=DistributedIngestionState.Processing;
        AttemptCount++;
        }
        public void Complete(){State=DistributedIngestionState.Completed;
        LastError=null;
        }
        public void Fail(string error){LastError=(error??"").Trim();
        State=AttemptCount>=5?DistributedIngestionState.DeadLetter:DistributedIngestionState.Failed;
        }
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
