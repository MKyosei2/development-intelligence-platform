// Purpose: Defines EvidenceEventType values.
namespace Dip.Domain.Enums;

public enum EvidenceEventType { Unknown = 0, SourceCommit = 1, SourcePullRequest = 2, SourceWorkflowRun = 3, FileCreated = 10, FileModified = 11, FileDeleted = 12, FileRenamed = 13, MayaSceneOpened = 20, MayaSceneSaved = 21, MayaSceneImported = 22, MayaReferenceChanged = 23, MayaExported = 24, MayaPlayblast = 25, UnityBuild = 30, UnrealBuild = 31, UnityAssetImported = 32, UnityTestsCompleted = 33, UnityProfilerSample = 34, UnrealCook = 35, UnrealPackage = 36, UnrealAutomationTests = 37, UnrealAssetChanged = 38, QaResult = 40, VisualRegression = 41, AssetQaFinding = 42, RenderCompleted = 50, ScreenshotGenerated = 51, VideoGenerated = 52, ProgressClaim = 60, ClaimAssessment = 61, ToolEvent = 70 }
