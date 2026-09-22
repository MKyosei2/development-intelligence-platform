// Purpose: Represents R el ea se Ev ID en ce Bu nd le in the DIP domain.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class ReleaseEvidenceBundle
{
    private ReleaseEvidenceBundle() { }
    public ReleaseEvidenceBundle(Guid id,Guid projectId,string commitSha,string buildId,ReleaseEvidenceBundleStatus status,string manifestArtifactId,string? sbomArtifactId,string? bundleArtifactId,string artifactSha256,DateTimeOffset createdAtUtc){if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        Id=id==Guid.Empty?Guid.NewGuid():id;
        ProjectId=projectId;
        CommitSha=Required(commitSha,64,nameof(commitSha));
        BuildId=Required(buildId,200,nameof(buildId));
        Status=status;
        ManifestArtifactId=Required(manifestArtifactId,64,nameof(manifestArtifactId));
        SbomArtifactId=Optional(sbomArtifactId,64);
        BundleArtifactId=Optional(bundleArtifactId,64);
        ArtifactSha256=Required(artifactSha256,64,nameof(artifactSha256));
        CreatedAtUtc=createdAtUtc;
        }
    public Guid Id{get;
        private set;
        }
        public Guid ProjectId{get;
        private set;
        }
        public string CommitSha{get;
        private set;
        }=string.Empty;
        public string BuildId{get;
        private set;
        }=string.Empty;
        public ReleaseEvidenceBundleStatus Status{get;
        private set;
        }
        public string ManifestArtifactId{get;
        private set;
        }=string.Empty;
        public string? SbomArtifactId{get;
        private set;
        }
        public string? BundleArtifactId{get;
        private set;
        }
        public string ArtifactSha256{get;
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
