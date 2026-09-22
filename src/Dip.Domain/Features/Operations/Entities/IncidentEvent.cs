// Purpose: Represents I nc ID en tE ve nt in the DIP domain.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class IncidentEvent
{
    private IncidentEvent() { }
    public IncidentEvent(Guid id,Guid incidentId,IncidentEventType type,string summary,string? payloadJson,DateTimeOffset occurredAtUtc)
    {if(incidentId==Guid.Empty)throw new ArgumentException("IncidentId is required.",nameof(incidentId));
        Id=id==Guid.Empty?Guid.NewGuid():id;
        IncidentId=incidentId;
        Type=type;
        Summary=Required(summary,2000);
        PayloadJson=Optional(payloadJson,32000);
        OccurredAtUtc=occurredAtUtc;
        }
    public Guid Id{get;
        private set;
        }
        public Guid IncidentId{get;
        private set;
        }
        public IncidentEventType Type{get;
        private set;
        }
        public string Summary{get;
        private set;
        }=string.Empty;
        public string? PayloadJson{get;
        private set;
        }
        public DateTimeOffset OccurredAtUtc{get;
        private set;
        }
    private static string Required(string? value,int max){var text=value?.Trim()??string.Empty;
        if(text.Length is <1||text.Length>max)throw new ArgumentException();
        return text;
        }
        private static string? Optional(string? value,int max)=>string.IsNullOrWhiteSpace(value)?null:(value.Trim().Length<=max?value.Trim():value.Trim()[..max]);
}
