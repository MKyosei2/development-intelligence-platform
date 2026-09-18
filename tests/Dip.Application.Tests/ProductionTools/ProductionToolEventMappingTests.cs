// Purpose: Verifies behavior and invariants covered by P ro du ct io nT oo lE ve nt Ma pp in gT es ts.
using Dip.Application.Features.ProductionTools;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProductionTools;

public sealed class ProductionToolEventMappingTests
{
    [Theory]
    [InlineData(ProductionToolSource.Maya, ProductionToolEventKind.SceneSaved, EvidenceSourceType.Maya, EvidenceEventType.MayaSceneSaved)]
    [InlineData(ProductionToolSource.Unity, ProductionToolEventKind.BuildCompleted, EvidenceSourceType.Unity, EvidenceEventType.UnityBuild)]
    [InlineData(ProductionToolSource.Unreal, ProductionToolEventKind.CookCompleted, EvidenceSourceType.Unreal, EvidenceEventType.UnrealCook)]
    [InlineData(ProductionToolSource.Unity, ProductionToolEventKind.ScreenshotGenerated, EvidenceSourceType.Unity, EvidenceEventType.ScreenshotGenerated)]
    public void Mapping_is_deterministic(
        ProductionToolSource source,
        ProductionToolEventKind kind,
        EvidenceSourceType expectedSource,
        EvidenceEventType expectedEvent)
    {
        ProductionToolEventMapping.SourceType(source).Should().Be(expectedSource);
        ProductionToolEventMapping.EventType(source, kind).Should().Be(expectedEvent);
    }
}
