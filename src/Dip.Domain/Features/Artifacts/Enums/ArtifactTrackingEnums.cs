// Purpose: Defines A rt if ac tT ra ck in gE nu ms states used by Artifacts.
namespace Dip.Domain.Enums;

public enum FileObservationKind
{
    Created = 0,
    Modified = 1,
    Deleted = 2,
    Renamed = 3
}

public enum TrackedArtifactLifecycleState
{
    Active = 0,
    Deleted = 1
}

public enum ArtifactPreviewKind
{
    Thumbnail = 0,
    Screenshot = 1,
    Video = 2,
    Render = 3
}

public enum ArtifactDependencyKind
{
    Unknown = 0,
    References = 1,
    Texture = 2,
    Material = 3,
    Prefab = 4,
    GeneratedFrom = 5
}
