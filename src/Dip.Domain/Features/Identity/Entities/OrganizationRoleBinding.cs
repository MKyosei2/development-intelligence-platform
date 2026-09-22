// Purpose: Defines OrganizationRoleBinding.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class OrganizationRoleBinding
{
    private OrganizationRoleBinding() { }
    public OrganizationRoleBinding(Guid id,Guid organizationId,string subjectId,OrganizationRole role,Guid? projectId,string grantedBy,DateTimeOffset now){Id=id==Guid.Empty?Guid.NewGuid():id;
        OrganizationId=organizationId;
        SubjectId=Req(subjectId,300);
        Role=role;
        ProjectId=projectId;
        GrantedBy=Req(grantedBy,240);
        GrantedAtUtc=now;
        }
    public Guid Id{get;
        private set;
        }
        public Guid OrganizationId{get;
        private set;
        }
        public string SubjectId{get;
        private set;
        }="";
        public OrganizationRole Role{get;
        private set;
        }
        public Guid? ProjectId{get;
        private set;
        }
        public string GrantedBy{get;
        private set;
        }="";
        public DateTimeOffset GrantedAtUtc{get;
        private set;
        }
    private static string Req(string? v,int m){var x=v?.Trim()??"";if(x.Length is <1||x.Length>m)throw new ArgumentException("Invalid value.");return x;}
}
