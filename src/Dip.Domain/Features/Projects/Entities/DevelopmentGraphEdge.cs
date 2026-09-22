// Purpose: Represents D ev el op me nt Gr ap hE dg e in the DIP domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class DevelopmentGraphEdge
{
    private DevelopmentGraphEdge() { }
    public DevelopmentGraphEdge(Guid id,Guid projectId,Guid sourceNodeId,Guid targetNodeId,DevelopmentGraphEdgeType type,decimal weight,bool critical,string metadataJson,DateTimeOffset observedAtUtc)
    {
        if(projectId==Guid.Empty||sourceNodeId==Guid.Empty||targetNodeId==Guid.Empty||sourceNodeId==targetNodeId)throw new ArgumentException("Project and distinct graph node IDs are required.");
        if(!Enum.IsDefined(type))throw new ArgumentOutOfRangeException(nameof(type));
            Id=id==Guid.Empty?Guid.NewGuid():id;
            ProjectId=projectId;
            SourceNodeId=sourceNodeId;
            TargetNodeId=targetNodeId;
            Type=type;
        Weight=Math.Clamp(weight,0m,100m);
            Critical=critical;
            MetadataJson=Required(metadataJson,32000,nameof(metadataJson));
            FirstObservedAtUtc=observedAtUtc;
            LastObservedAtUtc=observedAtUtc;
    }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public Guid SourceNodeId{get;
        private set;
        }
        public Guid TargetNodeId{get;
        private set;
        }
        public DevelopmentGraphEdgeType Type{get;
        private set;
        }
    public decimal Weight{get;
        private set;
        }
        public bool Critical{get;
        private set;
        }
        public string MetadataJson{get;
        private set;
        }="{}";
        public DateTimeOffset FirstObservedAtUtc{get;
        private set;
        }
        public DateTimeOffset LastObservedAtUtc{get;
        private set;
        }
    public void Observe(decimal weight,bool critical,string metadataJson,DateTimeOffset observedAtUtc){Weight=Math.Clamp(weight,0m,100m);
        Critical=critical;
        MetadataJson=Required(metadataJson,32000,nameof(metadataJson));
        if(observedAtUtc>LastObservedAtUtc)LastObservedAtUtc=observedAtUtc;
        }
    private static string Required(string? value,int max,string name){var v=value?.Trim()??string.Empty;
        if(v.Length is <1||v.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);
        return v;
        }
}
