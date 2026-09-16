// Purpose: Defines E vi de nc eI nt eg ra ti on En um s states used by Evidence.
namespace Dip.Domain.Enums;

public enum EvidenceEventType
{
    Unknown = 0,
    SourceCommit = 1,
    SourcePullRequest = 2,
    SourceWorkflowRun = 3,
    FileCreated = 10,
    FileModified = 11,
    FileDeleted = 12,
    FileRenamed = 13,
    MayaSceneOpened = 20,
    MayaSceneSaved = 21,
    MayaSceneImported = 22,
    MayaReferenceChanged = 23,
    MayaExported = 24,
    MayaPlayblast = 25,
    UnityBuild = 30,
    UnrealBuild = 31,
    UnityAssetImported = 32,
    UnityTestsCompleted = 33,
    UnityProfilerSample = 34,
    UnrealCook = 35,
    UnrealPackage = 36,
    UnrealAutomationTests = 37,
    UnrealAssetChanged = 38,
    QaResult = 40,
    VisualRegression = 41,
    AssetQaFinding = 42,
    RenderCompleted = 50,
    ScreenshotGenerated = 51,
    VideoGenerated = 52,
    ProgressClaim = 60,
    ClaimAssessment = 61,
    ToolEvent = 70
}

public enum EvidencePrivacyClass
{
    Internal = 0,
    ProjectRestricted = 1,
    Sensitive = 2
}

public enum EvidenceIntegrityStatus
{
    Unverified = 0,
    Verified = 1,
    Invalid = 2,
    LegacyUnsigned = 3
}

public enum IntegrationConnectionMode
{
    Unknown = 0,
    GitHubPat = 1,
    GitHubApp = 2,
    GitHubEnterprisePat = 3,
    GitHubEnterpriseApp = 4
}

public enum ConnectionHealthState
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unauthorized = 3,
    Unavailable = 4,
    Misconfigured = 5,
    Disabled = 6
}

public enum PolicyScopeType
{
    Global = 0,
    Project = 1,
    Agent = 2
}
