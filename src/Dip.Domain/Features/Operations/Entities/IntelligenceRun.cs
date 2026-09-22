// Purpose: Represents I nt el li ge nc eR un in the DIP domain.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class IntelligenceRun
{
    private IntelligenceRun() { }
    public IntelligenceRun(Guid id,Guid projectId,IntelligenceRunType type,string subjectKey,string artifactRoot,string inputJson,DateTimeOffset createdAtUtc)
    { if(projectId==Guid.Empty)throw new ArgumentException(nameof(projectId));
        Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        Type=type;
        SubjectKey=Req(subjectKey,200);
        ArtifactRoot=Req(artifactRoot,1000);
        InputJson=Req(inputJson,32000);
        Status=IntelligenceRunStatus.Queued;
        CreatedAtUtc=createdAtUtc;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public IntelligenceRunType Type{get;
        private set;
        }
        public string SubjectKey{get;
        private set;
        }=string.Empty;
        public string ArtifactRoot{get;
        private set;
        }=string.Empty;
        public string InputJson{get;
        private set;
        }=string.Empty;
        public string? ResultJson{get;
        private set;
        }
        public IntelligenceRunStatus Status{get;
        private set;
        }
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
        public DateTimeOffset? StartedAtUtc{get;
        private set;
        }
        public DateTimeOffset? FinishedAtUtc{get;
        private set;
        }
    public void Start(DateTimeOffset at){if(Status!=IntelligenceRunStatus.Queued)throw new InvalidOperationException();Status=IntelligenceRunStatus.Running;StartedAtUtc=at;}
    public void Complete(IntelligenceRunStatus status,string resultJson,DateTimeOffset at){if(Status!=IntelligenceRunStatus.Running)throw new InvalidOperationException();
        Status=status;
        ResultJson=Req(resultJson,32000);
        FinishedAtUtc=at;
        }
    static string Req(string?x,int m){var s=x?.Trim()??"";if(s.Length<1||s.Length>m)throw new ArgumentException();return s;}
}
