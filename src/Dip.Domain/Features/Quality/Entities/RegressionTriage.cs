// Purpose: Represents R eg re ss io nT ri ag e in the DIP domain.
namespace Dip.Domain.Entities;
public sealed class RegressionTriage
{
    private RegressionTriage() { }
    public RegressionTriage(Guid id,Guid projectId,Guid incidentId,string? suspectedCommit,string? suspectedSubsystem,string suspectedFilesJson,string? suggestedOwner,decimal confidence,string evidenceIdsJson,string explanation,DateTimeOffset createdAtUtc){if(projectId==Guid.Empty||incidentId==Guid.Empty)throw new ArgumentException("Project and incident IDs are required.");
        Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        IncidentId=incidentId;
        SuspectedCommit=Optional(suspectedCommit,64);
        SuspectedSubsystem=Optional(suspectedSubsystem,200);
        SuspectedFilesJson=Required(suspectedFilesJson,16000,nameof(suspectedFilesJson));
        SuggestedOwner=Optional(suggestedOwner,200);
        Confidence=Math.Clamp(confidence,0m,100m);
        EvidenceIdsJson=Required(evidenceIdsJson,16000,nameof(evidenceIdsJson));
        Explanation=Required(explanation,4000,nameof(explanation));
        CreatedAtUtc=createdAtUtc;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid IncidentId{get;
        private set;
        }
        public string? SuspectedCommit{get;
        private set;
        }
        public string? SuspectedSubsystem{get;
        private set;
        }
        public string SuspectedFilesJson{get;
        private set;
        }="[]";
        public string? SuggestedOwner{get;
        private set;
        }
        public decimal Confidence{get;
        private set;
        }
        public string EvidenceIdsJson{get;
        private set;
        }="[]";
        public string Explanation{get;
        private set;
        }=string.Empty;
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
    private static string Required(string? value,int max,string name){var v=value?.Trim()??string.Empty;
        if(v.Length is <1||v.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);
        return v;
        }
        private static string? Optional(string? value,int max)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().Length<=max?value.Trim():value.Trim()[..max];
}
