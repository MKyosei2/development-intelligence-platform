// Purpose: Represents E xt er na lW or kI te mL in k in the DIP domain.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class ExternalWorkItemLink
{
    private ExternalWorkItemLink() { }
    public ExternalWorkItemLink(Guid id,Guid projectId,Guid incidentId,ExternalWorkItemProvider provider,string externalId,string url,string title,ExternalWorkItemStatus status,DateTimeOffset createdAtUtc){if(projectId==Guid.Empty||incidentId==Guid.Empty)throw new ArgumentException("Project and incident IDs are required.");
        Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        IncidentId=incidentId;
        Provider=provider;
        ExternalId=Required(externalId,200,nameof(externalId));
        Url=Required(url,2000,nameof(url));
        Title=Required(title,300,nameof(title));
        Status=status;
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
        public ExternalWorkItemProvider Provider{get;
        private set;
        }
        public string ExternalId{get;
        private set;
        }=string.Empty;
        public string Url{get;
        private set;
        }=string.Empty;
        public string Title{get;
        private set;
        }=string.Empty;
        public ExternalWorkItemStatus Status{get;
        private set;
        }
        public DateTimeOffset CreatedAtUtc{get;
        private set;
        }
    private static string Required(string? value,int max,string name){var v=value?.Trim()??string.Empty;
        if(v.Length is <1||v.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);
        return v;
        }
}
