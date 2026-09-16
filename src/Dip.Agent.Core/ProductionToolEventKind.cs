// Purpose: Defines ProductionToolEventKind values.
using System.Text.Json;

namespace Dip.Agent.Core;

public enum ProductionToolEventKind
{
    Unknown = 0,
    SceneOpened = 1,
    SceneSaved = 2,
    Imported = 3,
    ReferenceChanged = 4,
    Exported = 5,
    RenderCompleted = 6,
    PlayblastCompleted = 7,
    BuildStarted = 20,
    BuildCompleted = 21,
    AssetImported = 22,
    TestsCompleted = 23,
    PerformanceCaptured = 24,
    CookCompleted = 30,
    PackageCompleted = 31,
    AutomationTestsCompleted = 32,
    ScreenshotGenerated = 40,
    VideoGenerated = 41
}
